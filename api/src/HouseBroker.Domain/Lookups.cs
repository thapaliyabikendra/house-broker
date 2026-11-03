namespace HouseBroker.Domain;

/// <summary>Lookup entity for property types.</summary>
public sealed class PropertyType
{
    private PropertyType()
    {
    }

    /// <summary>Initializes a property type.</summary>
    public static PropertyType Create(string code, string name, int sortOrder)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            Name = name,
            SortOrder = sortOrder,
        };

    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the code.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Gets the name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the sort order.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Gets the created timestamp.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Gets the creator.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>Gets the updated timestamp.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Gets the updater.</summary>
    public Guid? UpdatedBy { get; private set; }
}

/// <summary>Lookup entity for features.</summary>
public sealed class Feature
{
    private Feature()
    {
    }

    /// <summary>Initializes a feature.</summary>
    public static Feature Create(string code, string name, int sortOrder)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            Name = name,
            SortOrder = sortOrder,
        };

    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the code.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Gets the name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the sort order.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Gets the created timestamp.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Gets the creator.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>Gets the updated timestamp.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Gets the updater.</summary>
    public Guid? UpdatedBy { get; private set; }
}