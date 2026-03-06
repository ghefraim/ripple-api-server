using Application.Common.Models.User;
using Application.Common.Validators;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Validators;

public class EmailLogInRequestValidatorTests
{
    private readonly EmailLogInRequestValidator _validator;

    public EmailLogInRequestValidatorTests()
    {
        _validator = new EmailLogInRequestValidator();
    }

    private static EmailLogInRequest CreateValidRequest() => new()
    {
        Email = "test@example.com",
        Password = "somepassword",
    };

    #region Email Validation

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@domain.co.uk")]
    [InlineData("user+tag@example.org")]
    public void Validate_WithValidEmail_ShouldNotHaveError(string email)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Email = email;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_WithEmptyEmail_ShouldHaveError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Email = string.Empty;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required.");
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("@nodomain.com")]
    public void Validate_WithInvalidEmail_ShouldHaveError(string email)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Email = email;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Invalid email format.");
    }

    #endregion

    #region Password Validation

    [Fact]
    public void Validate_WithValidPassword_ShouldNotHaveError()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_WithEmptyPassword_ShouldHaveError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Password = string.Empty;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required.");
    }

    [Fact]
    public void Validate_WithAnyPassword_ShouldNotHaveComplexityRequirements()
    {
        // Note: Login only requires password not to be empty
        // Complexity is only enforced during signup

        // Arrange
        var request = CreateValidRequest();
        request.Password = "simple"; // No complexity requirements

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    #endregion

    #region Full Validation

    [Fact]
    public void Validate_WithAllValidFields_ShouldPass()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var request = new EmailLogInRequest
        {
            Email = "invalid",
            Password = string.Empty,
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    #endregion
}
