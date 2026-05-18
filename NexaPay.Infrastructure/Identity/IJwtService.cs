// ============================================================
// IJwtService.cs – NexaPay.Infrastructure/Identity
// ============================================================
// Litet internt kontrakt mot JwtService. Lever inom Infrastructure
// (inte i Application) eftersom JWT-detaljer är en infrastruktur-
// implementation och inte en domänregel.
//
// TokenResult bär bara strängen + dess utgångstid – allt klienten
// behöver för att veta när nästa login krävs.
// ============================================================

namespace NexaPay.Infrastructure.Identity
{
    public record TokenResult(string Token, DateTime ExpiresAt);

    public interface IJwtService
    {
        TokenResult GenerateToken(string userId, string email, string role);
    }
}
