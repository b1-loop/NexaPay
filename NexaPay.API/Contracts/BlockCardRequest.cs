namespace NexaPay.API.Contracts
{
    public record BlockCardRequest
    {
        public string Reason { get; init; } = string.Empty;
    }
}
