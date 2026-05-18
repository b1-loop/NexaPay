// ============================================================
// LoginRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/auth/login.
// Controllern mappar den till LoginCommand som skickas in i MediatR.
// ============================================================

namespace NexaPay.API.Contracts
{
    public record LoginRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }
}
