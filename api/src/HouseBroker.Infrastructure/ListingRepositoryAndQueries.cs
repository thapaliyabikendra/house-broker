using HouseBroker.Application.Contracts.Listings;
using HouseBroker.Application.Abstractions;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HouseBroker.Infrastructure;

/// <summary>Listing repository implementation.</summary>
public sealed class ListingRepository(AppDbContext dbContext) : IListingRepository
{
    private readonly AppDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Listings
            .AsSplitQuery()
            .Include(listing => listing.Images)
            .Include(listing => listing.Features)
                .ThenInclude(feature => feature.Feature)
            .FirstOrDefaultAsync(listing => listing.Id == id && !listing.IsDeleted, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Listing listing, CancellationToken cancellationToken)
        => await _dbContext.Listings.AddAsync(listing, cancellationToken);

    /// <inheritdoc />
    public Task RemoveAsync(Listing listing, CancellationToken cancellationToken)
    {
        listing.SoftDelete();
        return Task.CompletedTask;
    }

    /// <summary>Adds a price history entry.</summary>
    public async Task AddPriceHistoryAsync(ListingPriceHistory priceHistory, CancellationToken cancellationToken)
        => await _dbContext.ListingPriceHistories.AddAsync(priceHistory, cancellationToken);

    /// <summary>Adds a status history entry.</summary>
    public async Task AddStatusHistoryAsync(ListingStatusHistory statusHistory, CancellationToken cancellationToken)
        => await _dbContext.ListingStatusHistories.AddAsync(statusHistory, cancellationToken);
}

/// <summary>Listing query implementation.</summary>
public sealed class ListingQueries(AppDbContext dbContext) : IListingQueries
{
    private readonly AppDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<Listing?> GetPublicAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Listings
            .AsNoTracking()
            .AsSplitQuery()
            .Include(listing => listing.Images)
            .Include(listing => listing.Features)
                .ThenInclude(feature => feature.Feature)
            .FirstOrDefaultAsync(listing => listing.Id == id && !listing.IsDeleted, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Listing>> GetMineAsync(Guid brokerId, CancellationToken cancellationToken)
        => await _dbContext.Listings
            .AsNoTracking()
            .AsSplitQuery()
            .IgnoreQueryFilters()
            .Where(listing => listing.BrokerId == brokerId && !listing.IsDeleted)
            .Include(listing => listing.Images)
            .Include(listing => listing.Features)
                .ThenInclude(feature => feature.Feature)
            .OrderByDescending(listing => listing.CreatedAt)
            .ThenBy(listing => listing.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResult<ListingSummaryDto>> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken)
    {
        var listings = _dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == ListingStatus.Active && !listing.IsDeleted);

        var filtered = listings;

        if (!string.IsNullOrWhiteSpace(query.Location))
        {
            var location = query.Location.Trim();
            filtered = filtered.Where(listing =>
                listing.City.Contains(location) ||
                listing.Locality.Contains(location) ||
                listing.Province.Contains(location));
        }

        if (query.MinPrice.HasValue)
        {
            filtered = filtered.Where(listing => listing.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            filtered = filtered.Where(listing => listing.Price <= query.MaxPrice.Value);
        }

        if (query.PropertyTypeId.HasValue)
        {
            filtered = filtered.Where(listing => listing.PropertyTypeId == query.PropertyTypeId.Value);
        }

        if (query.Bedrooms.HasValue)
        {
            filtered = filtered.Where(listing => listing.Bedrooms >= query.Bedrooms.Value);
        }

        if (query.FeatureIds is not null && query.FeatureIds.Count > 0)
        {
            foreach (var featureId in query.FeatureIds.Distinct())
            {
                filtered = filtered.Where(listing => listing.Features.Any(feature => feature.FeatureId == featureId));
            }
        }

        var total = await filtered.CountAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IOrderedQueryable<Listing> ordered = query.Sort?.ToLowerInvariant() switch
        {
            "price_asc" => filtered.OrderBy(listing => listing.Price).ThenBy(listing => listing.Id),
            "price_desc" => filtered.OrderByDescending(listing => listing.Price).ThenBy(listing => listing.Id),
            "oldest" => filtered.OrderBy(listing => listing.CreatedAt).ThenBy(listing => listing.Id),
            _ => filtered.OrderByDescending(listing => listing.CreatedAt).ThenBy(listing => listing.Id),
        };

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(listing => new ListingSummaryDto
            {
                Id = listing.Id,
                Title = listing.Title,
                Price = listing.Price,
                City = listing.City,
                Locality = listing.Locality,
                Bedrooms = listing.Bedrooms,
                Bathrooms = listing.Bathrooms,
                Status = listing.Status.ToString(),
                PropertyTypeId = listing.PropertyTypeId,
                PrimaryImageId = listing.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => (Guid?)image.Id)
                    .FirstOrDefault(),
                CreatedAt = listing.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ListingSummaryDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <inheritdoc />
    public Task<HouseBroker.Domain.BrokerProfile?> GetBrokerProfileAsync(Guid brokerId, CancellationToken cancellationToken)
        => _dbContext.BrokerProfiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.BrokerId == brokerId, cancellationToken);

    /// <inheritdoc />
    public async Task UpsertBrokerProfileAsync(HouseBroker.Domain.BrokerProfile profile, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.BrokerProfiles.FirstOrDefaultAsync(item => item.BrokerId == profile.BrokerId, cancellationToken);
        if (existing is null)
        {
            await _dbContext.BrokerProfiles.AddAsync(profile, cancellationToken);
        }
        else
        {
            _dbContext.Entry(existing).CurrentValues.SetValues(profile);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HouseBroker.Domain.PropertyType>> GetPropertyTypesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.PropertyTypes.AsNoTracking().OrderBy(item => item.SortOrder).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HouseBroker.Domain.Feature>> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Features.AsNoTracking().OrderBy(item => item.SortOrder).ToListAsync(cancellationToken);
    }
}
