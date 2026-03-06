namespace Application.UnitTests.Common.Builders;

public class EntitlementBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _organizationId = Guid.NewGuid();
    private string _featureKey = "maxTodoLists";
    private string _featureType = "limit";
    private string _value = "10";
    private EntitlementSource _source = EntitlementSource.Plan;
    private Guid? _sourceId;
    private DateTime? _expiresAt;
    private string? _grantedBy;
    private string? _reason;
    private DateTime _createdOn = DateTime.UtcNow;
    private bool _isDeleted;

    public EntitlementBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public EntitlementBuilder WithOrganizationId(Guid organizationId)
    {
        _organizationId = organizationId;
        return this;
    }

    public EntitlementBuilder WithFeatureKey(string featureKey)
    {
        _featureKey = featureKey;
        return this;
    }

    public EntitlementBuilder WithFeatureType(string featureType)
    {
        _featureType = featureType;
        return this;
    }

    public EntitlementBuilder WithValue(string value)
    {
        _value = value;
        return this;
    }

    public EntitlementBuilder WithSource(EntitlementSource source)
    {
        _source = source;
        return this;
    }

    public EntitlementBuilder WithSourceId(Guid? sourceId)
    {
        _sourceId = sourceId;
        return this;
    }

    public EntitlementBuilder WithExpiresAt(DateTime? expiresAt)
    {
        _expiresAt = expiresAt;
        return this;
    }

    public EntitlementBuilder WithGrantedBy(string? grantedBy)
    {
        _grantedBy = grantedBy;
        return this;
    }

    public EntitlementBuilder WithReason(string? reason)
    {
        _reason = reason;
        return this;
    }

    public EntitlementBuilder WithCreatedOn(DateTime createdOn)
    {
        _createdOn = createdOn;
        return this;
    }

    public EntitlementBuilder WithIsDeleted(bool isDeleted)
    {
        _isDeleted = isDeleted;
        return this;
    }

    public Entitlement Build()
    {
        return new Entitlement
        {
            Id = _id,
            OrganizationId = _organizationId,
            FeatureKey = _featureKey,
            FeatureType = _featureType,
            Value = _value,
            Source = _source,
            SourceId = _sourceId,
            ExpiresAt = _expiresAt,
            GrantedBy = _grantedBy,
            Reason = _reason,
            CreatedOn = _createdOn,
            IsDeleted = _isDeleted,
        };
    }

    public static EntitlementBuilder Default() => new();

    public static EntitlementBuilder ForLimit(string featureKey, int limit)
    {
        return new EntitlementBuilder()
            .WithFeatureKey(featureKey)
            .WithFeatureType("limit")
            .WithValue(limit.ToString());
    }

    public static EntitlementBuilder ForAccess(string featureKey, bool hasAccess = true)
    {
        return new EntitlementBuilder()
            .WithFeatureKey(featureKey)
            .WithFeatureType("access")
            .WithValue(hasAccess ? "true" : "false");
    }

    public static EntitlementBuilder Unlimited(string featureKey)
    {
        return new EntitlementBuilder()
            .WithFeatureKey(featureKey)
            .WithFeatureType("limit")
            .WithValue("unlimited");
    }
}
