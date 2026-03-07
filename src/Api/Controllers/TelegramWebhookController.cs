using System.Text.Json;

using Application.Common.Interfaces;
using Application.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelegramWebhookController : ControllerBase
{
    private readonly ITelegramNotifier _notifier;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        ITelegramNotifier notifier,
        ApplicationDbContext context,
        ILogger<TelegramWebhookController> logger)
    {
        _notifier = notifier;
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleUpdate([FromBody] JsonElement update, CancellationToken cancellationToken)
    {
        try
        {
            if (!update.TryGetProperty("message", out var message))
                return Ok();

            if (!message.TryGetProperty("chat", out var chat) || !chat.TryGetProperty("id", out var chatIdEl))
                return Ok();

            var chatId = chatIdEl.GetInt64();
            var text = message.TryGetProperty("text", out var textEl) ? textEl.GetString() ?? "" : "";

            if (text.StartsWith("/start"))
            {
                await HandleStartCommand(chatId, text, cancellationToken);
            }
            else if (text.StartsWith("/link"))
            {
                await HandleLinkCommand(chatId, text, cancellationToken);
            }
            else if (text.StartsWith("/status"))
            {
                await HandleStatusCommand(chatId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Telegram webhook update");
        }

        return Ok();
    }

    private async Task HandleStartCommand(long chatId, string text, CancellationToken cancellationToken)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            await _notifier.LinkChatAsync(chatId, parts[1], cancellationToken);
        }
        else
        {
            await SendMessage(chatId,
                "Welcome to <b>Ripple Alerts</b>!\n\n" +
                "To link your account, use:\n" +
                "<code>/link your_phone_number</code>\n\n" +
                "Example: <code>/link +40712345678</code>",
                cancellationToken);
        }
    }

    private async Task HandleLinkCommand(long chatId, string text, CancellationToken cancellationToken)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            await SendMessage(chatId,
                "Please provide your phone number:\n<code>/link +40712345678</code>",
                cancellationToken);
            return;
        }

        await _notifier.LinkChatAsync(chatId, parts[1], cancellationToken);
    }

    private async Task HandleStatusCommand(long chatId, CancellationToken cancellationToken)
    {
        var contact = await _context.CrewContacts
            .Include(c => c.Crew)
            .FirstOrDefaultAsync(c => c.TelegramChatId == chatId, cancellationToken);

        if (contact == null)
        {
            await SendMessage(chatId,
                "You are not linked to any crew.\nUse <code>/link your_phone</code> to get started.",
                cancellationToken);
        }
        else
        {
            await SendMessage(chatId,
                $"Linked as: <b>{contact.Name}</b>\n" +
                $"Crew: <b>{contact.Crew?.Name ?? "Unknown"}</b>\n" +
                $"Phone: {contact.PhoneNumber}",
                cancellationToken);
        }
    }

    private async Task SendMessage(long chatId, string text, CancellationToken cancellationToken)
    {
        if (!_notifier.IsConfigured) return;

        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var botToken = config.GetValue<string>("Telegram:BotToken");
        if (string.IsNullOrEmpty(botToken)) return;

        var httpClient = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Telegram");
        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

        await httpClient.PostAsJsonAsync(url, new { chat_id = chatId, text, parse_mode = "HTML" }, cancellationToken);
    }
}
