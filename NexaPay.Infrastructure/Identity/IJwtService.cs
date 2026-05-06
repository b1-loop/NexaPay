// ============================================================
// IJwtService.cs – NexaPay.Infrastructure/Identity
// ============================================================

namespace NexaPay.Infrastructure.Identity
{
    public record TokenResult(string Token, DateTime ExpiresAt);

    public interface IJwtService
    {
        TokenResult GenerateToken(string userId, string email, string role);
    }
}
