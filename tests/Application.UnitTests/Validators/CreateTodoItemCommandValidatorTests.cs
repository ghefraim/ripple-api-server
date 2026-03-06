using Application.Features.TodoItems.CreateTodoItem;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Validators;

public class CreateTodoItemCommandValidatorTests
{
    private readonly CreateTodoItemCommandValidator _validator;
    private readonly Guid _validListId = Guid.NewGuid();

    public CreateTodoItemCommandValidatorTests()
    {
        _validator = new CreateTodoItemCommandValidator();
    }

    #region ListId Validation

    [Fact]
    public void Validate_WithValidListId_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateTodoItemCommand(_validListId, "Test", null, PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ListId);
    }

    [Fact]
    public void Validate_WithEmptyListId_ShouldHaveError()
    {
        // Arrange
        var command = new CreateTodoItemCommand(Guid.Empty, "Test", null, PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ListId)
            .WithErrorMessage("Invalid list ID.");
    }

    #endregion

    #region Title Validation

    [Fact]
    public void Validate_WithValidTitle_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateTodoItemCommand(_validListId, "Buy groceries", null, PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithEmptyTitle_ShouldHaveError()
    {
        // Arrange
        var command = new CreateTodoItemCommand(_validListId, string.Empty, null, PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title is required.");
    }

    [Fact]
    public void Validate_WithTitleExceeding200Characters_ShouldHaveError()
    {
        // Arrange
        var longTitle = new string('A', 201);
        var command = new CreateTodoItemCommand(_validListId, longTitle, null, PriorityLevel.None, null, null, null);

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
        var command = new CreateTodoItemCommand(_validListId, exactTitle, null, PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithHtmlInTitle_ShouldHaveError()
    {
        // Arrange
        var command = new CreateTodoItemCommand(_validListId, "<script>alert('xss')</script>", null, PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    #endregion

    #region Note Validation

    [Fact]
    public void Validate_WithValidNote_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateTodoItemCommand(_validListId, "Test", "Some notes", PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Note);
    }

    [Fact]
    public void Validate_WithNullNote_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateTodoItemCommand(_validListId, "Test", null, PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Note);
    }

    [Fact]
    public void Validate_WithEmptyNote_ShouldNotHaveError()
    {
        // Arrange
        var command = new CreateTodoItemCommand(_validListId, "Test", string.Empty, PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Note);
    }

    [Fact]
    public void Validate_WithNoteExceeding2000Characters_ShouldHaveError()
    {
        // Arrange
        var longNote = new string('A', 2001);
        var command = new CreateTodoItemCommand(_validListId, "Test", longNote, PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Note)
            .WithErrorMessage("Note must not exceed 2000 characters.");
    }

    [Fact]
    public void Validate_WithNoteAt2000Characters_ShouldNotHaveError()
    {
        // Arrange
        var exactNote = new string('A', 2000);
        var command = new CreateTodoItemCommand(_validListId, "Test", exactNote, PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Note);
    }

    [Fact]
    public void Validate_WithHtmlInNote_ShouldHaveError()
    {
        // Arrange
        var command = new CreateTodoItemCommand(_validListId, "Test", "<img onerror='alert()'>", PriorityLevel.None, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Note);
    }

    #endregion

    #region Priority Validation

    [Theory]
    [InlineData(PriorityLevel.None)]
    [InlineData(PriorityLevel.Low)]
    [InlineData(PriorityLevel.Medium)]
    [InlineData(PriorityLevel.High)]
    public void Validate_WithValidPriority_ShouldNotHaveError(PriorityLevel priority)
    {
        // Arrange
        var command = new CreateTodoItemCommand(_validListId, "Test", null, priority, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }

    [Fact]
    public void Validate_WithInvalidPriority_ShouldHaveError()
    {
        // Arrange
        var command = new CreateTodoItemCommand(_validListId, "Test", null, (PriorityLevel)99, null, null, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Priority)
            .WithErrorMessage("Invalid priority level.");
    }

    #endregion

    #region Full Validation

    [Fact]
    public void Validate_WithAllValidFields_ShouldPass()
    {
        // Arrange
        var command = new CreateTodoItemCommand(
            _validListId,
            "Buy groceries",
            "Milk, eggs, bread",
            PriorityLevel.High,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var command = new CreateTodoItemCommand(
            Guid.Empty,
            string.Empty,
            new string('A', 2001),
            (PriorityLevel)99,
            null,
            null,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ListId);
        result.ShouldHaveValidationErrorFor(x => x.Title);
        result.ShouldHaveValidationErrorFor(x => x.Note);
        result.ShouldHaveValidationErrorFor(x => x.Priority);
    }

    #endregion
}
