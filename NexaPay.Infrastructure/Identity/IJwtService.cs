// ============================================================
// IJwtService.cs – NexaPay.Infrastructure/Identity
// ============================================================

namespace NexaPay.Infrastructure.Identity
{
    public interface IJwtService
    {
        string GenerateToken(string userId, string email, string role);
    }
}
