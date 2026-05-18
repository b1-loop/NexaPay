// ============================================================
// ChangePasswordRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/auth/change-password. Kräver inloggning.
// Aktuellt lösenord verifieras före byte (skydd mot session-hijack).
// ============================================================

namespace NexaPay.API.Contracts
{
    public record ChangePasswordRequest
    {
        public string CurrentPassword { get; init; } = string.Empty;
        public string NewPassword { get; init; } = string.Empty;
    }
}
