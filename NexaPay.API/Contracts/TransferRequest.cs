namespace NexaPay.API.Contracts
{
    public record TransferRequest
    {
        public Guid FromAccountId { get; init; }
        public Guid ToAccountId { get; init; }
        public decimal Amount { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}
