// ============================================================
// IAuthService.cs – NexaPay.Application/Common/Interfaces
// ============================================================
// Abstraktion över ASP.NET Identity + JWT. Application-handlers
// (RegisterHandler, LoginHandler, ResetPasswordHandler m.fl.)
// vet inget om Identity – de kallar IAuthService.
//
// Implementeras av AuthService i Infrastructure-lagret som
// internt använder UserManager, SignInManager och JwtService.
// ============================================================

using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Common.Interfaces
{
    public interface IAuthService
    {
        Task<Result<AuthDto>> RegisterAsync(string email, string password, string role, bool skipEmailConfirmation = false);
        Task<Result<AuthDto>> LoginAsync(string email, string password);
        Task<Result> ConfirmEmailAsync(string userId, string token);
        Task<Result> ForgotPasswordAsync(string email);
        Task<Result> ResetPasswordAsync(string email, string token, string newPassword);
        Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
    }
}
