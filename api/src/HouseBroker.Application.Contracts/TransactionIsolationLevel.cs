namespace HouseBroker.Application.Contracts;

/// <summary>Transaction isolation levels supported by the application.</summary>
public enum TransactionIsolationLevel
{
    /// <summary>Uncommitted reads.</summary>
    ReadUncommitted,

    /// <summary>Committed reads.</summary>
    ReadCommitted,

    /// <summary>Repeatable reads.</summary>
    RepeatableRead,

    /// <summary>Serializable reads.</summary>
    Serializable,

    /// <summary>Snapshot isolation.</summary>
    Snapshot,
}
