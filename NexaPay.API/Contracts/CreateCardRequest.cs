namespace NexaPay.API.Contracts
{
    public record CreateCardRequest
    {
        public Guid AccountId { get; init; }
        public string CardHolderName { get; init; } = string.Empty;
    }
}
