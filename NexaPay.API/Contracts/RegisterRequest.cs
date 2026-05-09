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
