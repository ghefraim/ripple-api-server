using System.Net.Http.Json;

using Application.Common.Interfaces;
using Application.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Infrastructure.Services;

public class TelegramNotifier : ITelegramNotifier
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TelegramNotifier> _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _botToken;

    public TelegramNotifier(
        ApplicationDbContext context,
        ILogger<TelegramNotifier> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Telegram");
        _botToken = configuration.GetValue<string>("Telegram:BotToken");
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_botToken);

    public async Task SendToGroupAsync(string crewName, Guid organizationId, string message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Telegram bot token not configured, skipping notification to crew '{Crew}'", crewName);
            return;
        }

        var contacts = await _context.CrewContacts
            .Include(c => c.Crew)
            .Where(c => c.Crew.Name == crewName
                && c.Crew.OrganizationId == organizationId
                && c.TelegramChatId.HasValue)
            .ToListAsync(cancellationToken);

        if (contacts.Count == 0)
        {
            _logger.LogWarning("No Telegram-linked contacts found for crew '{Crew}' in org {OrgId}", crewName, organizationId);
            return;
        }

        _logger.LogInformation("Sending Telegram notification to {Count} contacts in crew '{Crew}'", contacts.Count, crewName);

        foreach (var contact in contacts)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
                var payload = new
                {
                    chat_id = contact.TelegramChatId!.Value,
                    text = message,
                    parse_mode = "HTML"
                };

                var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Telegram API error for contact {Contact}: {Status} - {Body}",
                        contact.Name, response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Telegram message to {Contact} (chatId: {ChatId})",
                    contact.Name, contact.TelegramChatId);
            }
        }
    }

    public async Task LinkChatAsync(long chatId, string phoneNumber, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);

        var contacts = await _context.CrewContacts
            .Include(c => c.Crew)
            .Where(c => c.TelegramChatId == null)
            .ToListAsync(cancellationToken);

        var match = contacts.FirstOrDefault(c => NormalizePhone(c.PhoneNumber) == normalizedPhone);

        if (match == null)
        {
            _logger.LogWarning("No unlinked crew contact found for phone {Phone}", phoneNumber);
            return;
        }

        match.TelegramChatId = chatId;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Linked Telegram chatId {ChatId} to contact {Contact} in crew '{Crew}'",
            chatId, match.Name, match.Crew?.Name);

        if (IsConfigured)
        {
            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
            var payload = new
            {
                chat_id = chatId,
                text = $"Linked successfully! You'll receive alerts for crew: <b>{match.Crew?.Name ?? "your crew"}</b>",
                parse_mode = "HTML"
            };

            try
            {
                await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send link confirmation to chatId {ChatId}", chatId);
            }
        }
    }

    private static string NormalizePhone(string phone)
    {
        return new string(phone.Where(char.IsDigit).ToArray());
    }
}
