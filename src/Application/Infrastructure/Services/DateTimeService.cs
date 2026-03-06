using Application.Common.Interfaces;

namespace Application.Infrastructure.Services;

/// <summary>
/// Service implementation for providing current date and time.
/// </summary>
public class DateTimeService : IDateTime
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    public DateTime Now => DateTime.UtcNow;
}