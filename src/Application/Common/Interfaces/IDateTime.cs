namespace Application.Common.Interfaces;

/// <summary>
/// Service for providing current date and time
/// </summary>
public interface IDateTime
{
    /// <summary>
    /// Gets the current UTC date and time
    /// </summary>
    DateTime Now { get; }
}