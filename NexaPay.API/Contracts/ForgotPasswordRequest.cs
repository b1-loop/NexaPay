// ============================================================
// ForgotPasswordRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/auth/forgot-password. Endpointen
// returnerar alltid 200 OK (även för okänd e-post) för att
// inte avslöja vilka adresser som finns.
// ============================================================

namespace NexaPay.API.Contracts
{
    public record ForgotPasswordRequest
    {
        public string Email { get; init; } = string.Empty;
    }
}
