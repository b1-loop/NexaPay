namespace NexaPay.Application.Common.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(string command, string userId, bool isSuccess, CancellationToken cancellationToken = default);
    }
}
