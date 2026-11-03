using HouseBroker.Application;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain;
using HouseBroker.Domain.Shared;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HouseBroker.Infrastructure;

/// <summary>Listing repository implementation.</summary>
public sealed class ListingRepository(AppDbContext dbContext) : IListingRepository
{
    private readonly AppDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Listings.FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Listing listing, CancellationToken cancellationToken)
        => await _dbContext.Listings.AddAsync(listing, cancellationToken);

    /// <inheritdoc />
    public Task RemoveAsync(Listing listing, CancellationToken cancellationToken)
    {
        listing.SoftDelete();
        return Task.CompletedTask;
    }
}

/// <summary>Listing query implementation.</summary>
public sealed class ListingQueries(AppDbContext dbContext) : IListingQueries
{
    private readonly AppDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<Listing?> GetPublicAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Listings
            .Include(listing => listing.Images)
            .Include(listing => listing.Features)
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Listing>> GetMineAsync(Guid brokerId, CancellationToken cancellationToken)
        => await _dbContext.Listings
            .IgnoreQueryFilters()
            .Where(listing => listing.BrokerId == brokerId && !listing.IsDeleted)
            .Include(listing => listing.Images)
            .Include(listing => listing.Features)
            .OrderByDescending(listing => listing.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResult<ListingSummaryDto>> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken)
    {
        var listings = _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Images)
            .Where(listing => listing.Status == ListingStatus.Active);

        var filtered = listings;

        if (!string.IsNullOrWhiteSpace(query.Location))
        {
            filtered = filtered.Where(listing =>
                listing.City.Contains(query.Location) ||
                listing.Locality.Contains(query.Location));
        }

        if (query.MinPrice.HasValue)
        {
            filtered = filtered.Where(listing => listing.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            filtered = filtered.Where(listing => listing.Price <= query.MaxPrice.Value);
        }

        var total = await filtered.CountAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await filtered
            .OrderByDescending(listing => listing.CreatedAt)
            .ThenBy(listing => listing.Id)
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
}