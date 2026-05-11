namespace NexaPay.API.Contracts
{
    public record ConfirmEmailRequest
    {
        public string UserId { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
    }
}
