using Application.Common.Models.User;
using Application.Common.Validators;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Validators;

public class EmailSignUpRequestValidatorTests
{
    private readonly EmailSignUpRequestValidator _validator;

    public EmailSignUpRequestValidatorTests()
    {
        _validator = new EmailSignUpRequestValidator();
    }

    private static EmailSignUpRequest CreateValidRequest() => new()
    {
        Email = "test@example.com",
        Password = "Password123!",
        ConfirmPassword = "Password123!",
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

    [Theory]
    [InlineData("Password123!")]
    [InlineData("SecurePass@1")]
    [InlineData("Complex_Pass123")]
    [InlineData("StrongP@ssw0rd")]
    public void Validate_WithValidPassword_ShouldNotHaveError(string password)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Password = password;
        request.ConfirmPassword = password;

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
    public void Validate_WithPasswordShorterThan8Characters_ShouldHaveError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Password = "Pa1!xyz"; // 7 characters

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters.");
    }

    [Fact]
    public void Validate_WithPasswordWithoutUppercase_ShouldHaveError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Password = "password123!"; // No uppercase

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Validate_WithPasswordWithoutLowercase_ShouldHaveError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Password = "PASSWORD123!"; // No lowercase

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void Validate_WithPasswordWithoutDigit_ShouldHaveError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Password = "Password!!"; // No digit

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    [Fact]
    public void Validate_WithPasswordWithoutSpecialCharacter_ShouldHaveError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Password = "Password123"; // No special character

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one special character.");
    }

    #endregion

    #region Confirm Password Validation

    [Fact]
    public void Validate_WithMatchingConfirmPassword_ShouldNotHaveError()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    [Fact]
    public void Validate_WithEmptyConfirmPassword_ShouldHaveError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ConfirmPassword = string.Empty;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
            .WithErrorMessage("Password confirmation is required.");
    }

    [Fact]
    public void Validate_WithMismatchedConfirmPassword_ShouldHaveError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ConfirmPassword = "DifferentPassword123!";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
            .WithErrorMessage("Passwords do not match.");
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
        var request = new EmailSignUpRequest
        {
            Email = "invalid",
            Password = "weak",
            ConfirmPassword = "different",
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    #endregion
}
