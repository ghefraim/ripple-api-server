using Application.Features.TodoLists.CreateTodoList;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Validators;

public class CreateTodoListCommandValidatorTests
{
    private readonly CreateTodoListCommandValidator _validator;

    public CreateTodoListCommandValidatorTests()
    {
        _validator = new CreateTodoListCommandValidator();
    }

    #region Title Validation

    [Fact]
    public void Validate_WithValidTitle_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateTodoListCommand("My Todo List", null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithEmptyTitle_ShouldHaveError()
    {
        // Arrange
        var command = new CreateTodoListCommand(string.Empty, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title is required.");
    }

    [Fact]
    public void Validate_WithNullTitle_ShouldHaveError()
    {
        // Arrange
        var command = new CreateTodoListCommand(null!, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithTitleExceeding200Characters_ShouldHaveError()
    {
        // Arrange
        var longTitle = new string('A', 201);
        var command = new CreateTodoListCommand(longTitle, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title must not exceed 200 characters.");
    }

    [Fact]
    public void Validate_WithTitleAt200Characters_ShouldNotHaveError()
    {
        // Arrange
        var exactTitle = new string('A', 200);
        var command = new CreateTodoListCommand(exactTitle, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithHtmlInTitle_ShouldHaveError()
    {
        // Arrange
        var command = new CreateTodoListCommand("<script>alert('xss')</script>", null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    #endregion

    #region Colour Validation

    [Theory]
    [InlineData("#FFFFFF")]
    [InlineData("#FF5733")]
    [InlineData("#FFC300")]
    [InlineData("#FFFF66")]
    [InlineData("#CCFF99")]
    [InlineData("#6666FF")]
    [InlineData("#9966CC")]
    [InlineData("#999999")]
    public void Validate_WithValidColour_ShouldNotHaveError(string colour)
    {
        // Arrange
        var command = new CreateTodoListCommand("Test", colour);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Colour);
    }

    [Fact]
    public void Validate_WithNullColour_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateTodoListCommand("Test", null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Colour);
    }

    [Fact]
    public void Validate_WithEmptyColour_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateTodoListCommand("Test", string.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Colour);
    }

    [Fact]
    public void Validate_WithInvalidColour_ShouldHaveError()
    {
        // Arrange
        var command = new CreateTodoListCommand("Test", "#000000");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Colour)
            .WithErrorMessage("Invalid colour. Supported hex codes: #FFFFFF, #FF5733, #FFC300, #FFFF66, #CCFF99, #6666FF, #9966CC, #999999.");
    }

    [Fact]
    public void Validate_WithColourCaseInsensitive_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateTodoListCommand("Test", "#ffffff");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Colour);
    }

    #endregion
}
