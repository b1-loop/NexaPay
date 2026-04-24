// ============================================================
// JwtService.cs – NexaPay.Infrastructure/Identity
// ============================================================
// Genererar JWT-tokens för autentiserade användare.
//
// En JWT-token innehåller:
//   - UserId (sub claim)
//   - Email
//   - Roll (Admin eller User)
//   - Utgångstid (exp claim)
//   - En digital signatur som garanterar äkthet
//
// Tokens signeras med en hemlig nyckel som bara servern känner till.
// Om någon försöker ändra token-innehållet ogiltigförklaras signaturen.
// ============================================================

using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NexaPay.Infrastructure.Identity
{
    // Interface för JwtService – för testbarhet och DI
    public interface IJwtService
    {
        // Genererar en JWT-token för en användare
        // Returnerar token-strängen som skickas till klienten
        string GenerateToken(string userId, string email, string role);
    }

    public class JwtService : IJwtService
    {
        // IConfiguration ger oss tillgång till appsettings.json
        // Där lagrar vi vår hemliga nyckel och inställningar
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(string userId, string email, string role)
        {
            // --------------------------------------------------------
            // Steg 1: Definiera Claims (anspråk)
            // --------------------------------------------------------
            // Claims är information som vi lägger i token-payloaden
            // Controllern kan sedan läsa dessa för att veta vem som anropar
            var claims = new[]
            {
                // Sub (subject) = vem token tillhör – standard JWT claim
                new Claim(JwtRegisteredClaimNames.Sub, userId),

                // Jti (JWT ID) = unikt ID för denna token
                // Kan användas för att blacklista specifika tokens
                new Claim(JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString()),

                // Email-claim
                new Claim(JwtRegisteredClaimNames.Email, email),

                // NameIdentifier = standard claim för användar-ID
                // Används av ASP.NET Core för User.FindFirstValue()
                new Claim(ClaimTypes.NameIdentifier, userId),

                // Role-claim – används för [Authorize(Roles = "Admin")]
                new Claim(ClaimTypes.Role, role)
            };

            // --------------------------------------------------------
            // Steg 2: Skapa signeringsnyckeln
            // --------------------------------------------------------
            // Hämta den hemliga nyckeln från appsettings.json
            // Nyckeln måste vara minst 256 bitar (32 tecken) för HS256
            var jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "JWT-nyckeln saknas i konfigurationen");

            // Konvertera strängen till bytes
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey));

            // Skapa signeringsuppgifter med HMAC-SHA256-algoritmen
            // HMAC-SHA256 är industristandard för JWT-signering
            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            // --------------------------------------------------------
            // Steg 3: Skapa token-beskrivningen
            // --------------------------------------------------------
            var tokenDescriptor = new JwtSecurityToken(
                // Issuer = vem som utfärdat token (vår applikation)
                issuer: _configuration["Jwt:Issuer"],

                // Audience = vem token är avsedd för (vår klient)
                audience: _configuration["Jwt:Audience"],

                // Claims vi definierade ovan
                claims: claims,

                // NotBefore = token är inte giltig före detta datum
                notBefore: DateTime.UtcNow,

                // Expires = token slutar gälla efter X timmar
                // Vi läser antalet timmar från konfigurationen
                expires: DateTime.UtcNow.AddHours(
                    double.Parse(
                        _configuration["Jwt:ExpiryHours"] ?? "24")),

                // Signeringsuppgifterna vi skapade ovan
                signingCredentials: credentials
            );

            // --------------------------------------------------------
            // Steg 4: Serialisera token till en sträng
            // --------------------------------------------------------
            // WriteToken konverterar JwtSecurityToken-objektet
            // till den faktiska token-strängen som skickas till klienten
            // T.ex. "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }
    }
}