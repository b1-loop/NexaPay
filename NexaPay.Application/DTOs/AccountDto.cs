// ============================================================
// AccountDto.cs – NexaPay.Application/DTOs
// ============================================================
// API-vänlig representation av Account-aggregatet. AutoMapper
// (MappingProfile) plattar ut Money till Balance + Currency och
// konverterar enums till strängar ("Open" istället för 0) för
// JSON-läsbarhet i frontend.
// ============================================================

namespace NexaPay.Application.DTOs
{
    public class AccountDto
    {
        public Guid Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string OwnerId { get; set; } = string.Empty;
    }
}
