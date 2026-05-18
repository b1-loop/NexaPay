// ============================================================
// AdminCreateUserRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/admin/users. Används av Admin för
// att skapa nya kunder eller personalanvändare. Skickas in i
// AuthService.RegisterAsync med skipEmailConfirmation=true så
// att admin-skapade konton är direkt aktiva.
// ============================================================

using NexaPay.Application.Common.Constants;

namespace NexaPay.API.Contracts
{
    public record AdminCreateUserRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string Role { get; init; } = Roles.User;
    }
}
