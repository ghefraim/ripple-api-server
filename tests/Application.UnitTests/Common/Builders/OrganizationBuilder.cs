namespace Application.UnitTests.Common.Builders;

public class OrganizationBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Organization";
    private string? _description;
    private DateTime _createdOn = DateTime.UtcNow;
    private string? _createdBy;

    public OrganizationBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public OrganizationBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public OrganizationBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public OrganizationBuilder WithCreatedOn(DateTime createdOn)
    {
        _createdOn = createdOn;
        return this;
    }

    public OrganizationBuilder WithCreatedBy(string? createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public Organization Build()
    {
        return new Organization
        {
            Id = _id,
            Name = _name,
            Description = _description,
            CreatedOn = _createdOn,
            CreatedBy = _createdBy,
        };
    }

    public static OrganizationBuilder Default() => new();
}
