namespace Application.UnitTests.Common.Builders;

public class ApplicationUserBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _email = "test@example.com";
    private string _userName = "testuser";
    private bool _emailConfirmed = true;
    private DateTime _createdOn = DateTime.UtcNow;

    public ApplicationUserBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ApplicationUserBuilder WithEmail(string email)
    {
        _email = email;
        _userName = email;
        return this;
    }

    public ApplicationUserBuilder WithUserName(string userName)
    {
        _userName = userName;
        return this;
    }

    public ApplicationUserBuilder WithEmailConfirmed(bool confirmed)
    {
        _emailConfirmed = confirmed;
        return this;
    }

    public ApplicationUserBuilder WithCreatedOn(DateTime createdOn)
    {
        _createdOn = createdOn;
        return this;
    }

    public ApplicationUser Build()
    {
        return new ApplicationUser
        {
            Id = _id,
            Email = _email,
            UserName = _userName,
            NormalizedEmail = _email.ToUpperInvariant(),
            NormalizedUserName = _userName.ToUpperInvariant(),
            EmailConfirmed = _emailConfirmed,
            CreatedOn = _createdOn,
        };
    }

    public static ApplicationUserBuilder Default() => new();
}
