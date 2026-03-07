namespace Application.Common.Interfaces;

public interface ITelegramNotifier
{
    Task SendToGroupAsync(string groupName, Guid organizationId, string message, CancellationToken cancellationToken = default);
    Task LinkChatAsync(long chatId, string phoneNumber, CancellationToken cancellationToken = default);
    bool IsConfigured { get; }
}
