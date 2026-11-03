namespace HouseBroker.Domain;

/// <summary>Status history entry.</summary>
public sealed class ListingStatusHistory
{
    private ListingStatusHistory()
    {
    }

    /// <summary>Initializes a status history entry.</summary>
    public static ListingStatusHistory Create(Guid listingId, Domain.Shared.ListingStatus? fromStatus, Domain.Shared.ListingStatus toStatus, Guid? changedBy, DateTime changedAt, string? reason)
        => new()
        {
            Id = Guid.CreateVersion7(),
            ListingId = listingId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedBy = changedBy,
            ChangedAt = changedAt,
            Reason = reason,
        };

    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the listing identifier.</summary>
    public Guid ListingId { get; private set; }

    /// <summary>Gets the previous status.</summary>
    public Domain.Shared.ListingStatus? FromStatus { get; private set; }

    /// <summary>Gets the new status.</summary>
    public Domain.Shared.ListingStatus ToStatus { get; private set; }

    /// <summary>Gets the actor.</summary>
    public Guid? ChangedBy { get; private set; }

    /// <summary>Gets the change timestamp.</summary>
    public DateTime ChangedAt { get; private set; }

    /// <summary>Gets the optional reason.</summary>
    public string? Reason { get; private set; }
}

/// <summary>Price history entry.</summary>
public sealed class ListingPriceHistory
{
    private ListingPriceHistory()
    {
    }

    /// <summary>Initializes a price history entry.</summary>
    public static ListingPriceHistory Create(
        Guid listingId,
        decimal oldPrice,
        decimal newPrice,
        decimal oldCommissionAmount,
        decimal newCommissionAmount,
        decimal oldCommissionRatePercent,
        decimal newCommissionRatePercent,
        Guid? oldCommissionRateId,
        Guid newCommissionRateId,
        Guid? changedBy,
        DateTime changedAt)
        => new()
        {
            Id = Guid.CreateVersion7(),
            ListingId = listingId,
            OldPrice = oldPrice,
            NewPrice = newPrice,
            OldCommissionAmount = oldCommissionAmount,
            NewCommissionAmount = newCommissionAmount,
            OldCommissionRatePercent = oldCommissionRatePercent,
            NewCommissionRatePercent = newCommissionRatePercent,
            OldCommissionRateId = oldCommissionRateId,
            NewCommissionRateId = newCommissionRateId,
            ChangedBy = changedBy,
            ChangedAt = changedAt,
        };

    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the listing identifier.</summary>
    public Guid ListingId { get; private set; }

    /// <summary>Gets the old price.</summary>
    public decimal OldPrice { get; private set; }

    /// <summary>Gets the new price.</summary>
    public decimal NewPrice { get; private set; }

    /// <summary>Gets the old commission amount.</summary>
    public decimal OldCommissionAmount { get; private set; }

    /// <summary>Gets the new commission amount.</summary>
    public decimal NewCommissionAmount { get; private set; }

    /// <summary>Gets the old commission rate percent.</summary>
    public decimal OldCommissionRatePercent { get; private set; }

    /// <summary>Gets the new commission rate percent.</summary>
    public decimal NewCommissionRatePercent { get; private set; }

    /// <summary>Gets the old commission rate identifier.</summary>
    public Guid? OldCommissionRateId { get; private set; }

    /// <summary>Gets the new commission rate identifier.</summary>
    public Guid NewCommissionRateId { get; private set; }

    /// <summary>Gets the actor.</summary>
    public Guid? ChangedBy { get; private set; }

    /// <summary>Gets the change timestamp.</summary>
    public DateTime ChangedAt { get; private set; }
}

/// <summary>Audit event entry.</summary>
public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    /// <summary>Initializes an audit event.</summary>
    public static AuditEvent Create(
        DateTime occurredAt,
        Guid? actorId,
        string actorType,
        Guid? actorClientId,
        string action,
        string entityType,
        Guid? entityId,
        Guid? correlationId,
        string? oldValues,
        string? newValues)
        => new()
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = occurredAt,
            ActorId = actorId,
            ActorType = actorType,
            ActorClientId = actorClientId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            CorrelationId = correlationId,
            OldValues = oldValues,
            NewValues = newValues,
        };

    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the occurrence timestamp.</summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>Gets the actor.</summary>
    public Guid? ActorId { get; private set; }

    /// <summary>Gets the actor type.</summary>
    public string ActorType { get; private set; } = string.Empty;

    /// <summary>Gets the actor client identifier.</summary>
    public Guid? ActorClientId { get; private set; }

    /// <summary>Gets the action.</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>Gets the entity type.</summary>
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>Gets the entity identifier.</summary>
    public Guid? EntityId { get; private set; }

    /// <summary>Gets the correlation identifier.</summary>
    public Guid? CorrelationId { get; private set; }

    /// <summary>Gets the old values.</summary>
    public string? OldValues { get; private set; }

    /// <summary>Gets the new values.</summary>
    public string? NewValues { get; private set; }
}
