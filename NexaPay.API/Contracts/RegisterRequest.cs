// ============================================================
// RegisterRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/auth/register. Default-rollen är User
// så att vanliga kundregistreringar bara behöver skicka e-post
// och lösenord. Personalroller måste väljas explicit och kräver
// en e-postadress som passerar StaffEmailPolicy.
// ============================================================

using NexaPay.Application.Common.Constants;

namespace NexaPay.API.Contracts
{
    public record RegisterRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string Role { get; init; } = Roles.User;
    }
}
