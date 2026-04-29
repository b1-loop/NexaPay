// ============================================================
// AuthDto.cs – NexaPay.Application/DTOs
// ============================================================
// Data Transfer Object för autentiseringssvar.
// Detta är vad klienten får tillbaka efter registrering
// och inloggning.
//
// Innehåller JWT-token och grundläggande användarinfo.
// Lösenordet skickas ALDRIG tillbaka till klienten.
// ============================================================

namespace NexaPay.Application.DTOs
{
    public class AuthDto
    {
        // JWT-token som klienten ska skicka med
        // i Authorization-headern för alla skyddade anrop
        // Format: "Bearer eyJhbGciOiJIUzI1NiIs..."
        public string Token { get; set; } = string.Empty;

        // Användarens e-postadress
        public string Email { get; set; } = string.Empty;

        // Användarens roll – "Admin" eller "User"
        // Klienten kan använda detta för att visa/dölja UI-element
        public string Role { get; set; } = string.Empty;

        // När token slutar gälla
        // Klienten kan använda detta för att veta när
        // den behöver logga in igen
        public DateTime ExpiresAt { get; set; }
    }
}