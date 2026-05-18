// ============================================================
// ConfirmEmailRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/auth/confirm-email. UserId + Token
// kommer från länken i bekräftelsemejlet (FrontendUrl/confirm-email).
// ============================================================

namespace NexaPay.API.Contracts
{
    public record ConfirmEmailRequest
    {
        public string UserId { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
    }
}
