using NexaPay.Domain.Enums;

namespace NexaPay.API.Contracts
{
    public record CreateAccountRequest
    {
        public string AccountName { get; init; } = string.Empty;
        public AccountType AccountType { get; init; }
    }
}
