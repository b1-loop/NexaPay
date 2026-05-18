// ============================================================
// JwtService.cs – NexaPay.Infrastructure/Identity
// ============================================================
// Skapar JWT-tokens (HS256) för inloggade användare. Varje token
// innehåller:
//   * sub  – användarens id (string)
//   * jti  – unik token-id (för revokering via ITokenDenylist)
//   * email
//   * role – exakt en av Roles.* – läses av [Authorize(Roles="…")]
//
// Nyckeln, issuer, audience och expiry tas från appsettings
// (sektion "Jwt"). Default-expiry är 24 timmar om värdet saknas.
// ============================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace NexaPay.Infrastructure.Identity
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtService> _logger;

        public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public TokenResult GenerateToken(string userId, string email, string role)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            };

            var jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "JWT-nyckeln saknas i konfigurationen");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            if (!double.TryParse(_configuration["Jwt:ExpiryHours"], out var hours))
            {
                _logger.LogWarning(
                    "Jwt:ExpiryHours saknas i konfigurationen. Använder default 24 timmar.");
                hours = 24;
            }

            var expires = DateTime.UtcNow.AddHours(hours);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                NotBefore = DateTime.UtcNow,
                Expires = expires,
                SigningCredentials = credentials
            };

            var tokenString = new JsonWebTokenHandler().CreateToken(tokenDescriptor);

            return new TokenResult(tokenString, expires);
        }
    }
}
