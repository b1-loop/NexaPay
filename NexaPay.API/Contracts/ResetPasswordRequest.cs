// ============================================================
// ResetPasswordRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/auth/reset-password. Token kommer från
// reset-länken i mejlet. Tokens är engångsbruk och utgår enligt
// Identity-konfigurationen (default 24 timmar).
// ============================================================

namespace NexaPay.API.Contracts
{
    public record ResetPasswordRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
        public string NewPassword { get; init; } = string.Empty;
    }
}
