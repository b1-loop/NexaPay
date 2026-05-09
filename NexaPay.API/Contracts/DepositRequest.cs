namespace NexaPay.API.Contracts
{
    public record DepositRequest
    {
        public Guid AccountId { get; init; }
        public decimal Amount { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}
