using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Common.Interfaces
{
    public interface IAuthService
    {
        Task<Result<AuthDto>> RegisterAsync(string email, string password, string role);
        Task<Result<AuthDto>> LoginAsync(string email, string password);
        Task<Result> ConfirmEmailAsync(string userId, string token);
        Task<Result> ForgotPasswordAsync(string email);
        Task<Result> ResetPasswordAsync(string email, string token, string newPassword);
        Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
    }
}
