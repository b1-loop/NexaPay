namespace NexaPay.API.Contracts
{
    public record ForgotPasswordRequest
    {
        public string Email { get; init; } = string.Empty;
    }
}
