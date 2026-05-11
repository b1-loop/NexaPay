namespace NexaPay.API.Contracts
{
    public record ResetPasswordRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
        public string NewPassword { get; init; } = string.Empty;
    }
}
