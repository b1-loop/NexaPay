namespace NexaPay.API.Contracts
{
    public record WithdrawRequest
    {
        public Guid AccountId { get; init; }
        public decimal Amount { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}
