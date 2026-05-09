namespace NexaPay.Infrastructure.Persistence
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string Command { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
