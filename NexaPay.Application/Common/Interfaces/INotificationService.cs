namespace NexaPay.Application.Common.Interfaces
{
    public interface INotificationService
    {
        Task NotifyTransactionAsync(string ownerId, string subject, string body, CancellationToken cancellationToken = default);
        Task NotifyCardBlockedAsync(string ownerId, Guid cardId, CancellationToken cancellationToken = default);
        Task NotifyAccountClosedAsync(string ownerId, Guid accountId, CancellationToken cancellationToken = default);
    }
}
