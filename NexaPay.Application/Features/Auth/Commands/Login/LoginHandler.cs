// ============================================================
// LoginHandler.cs
// NexaPay.Application/Features/Auth/Commands/Login
// ============================================================
// Hanterar LoginCommand.
// Precis som RegisterHandler känner denna bara till
// IAuthService – ingenting om Identity eller databaser.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Auth.Commands.Login
{
    public class LoginHandler
        : IRequestHandler<LoginCommand, Result<AuthDto>>
    {
        private readonly IAuthService _authService;

        public LoginHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<Result<AuthDto>> Handle(
            LoginCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Delegera till AuthService i Infrastructure
                return await _authService.LoginAsync(
                    request.Email,
                    request.Password);
            }
            catch (Exception ex)
            {
                return Result<AuthDto>.Failure(
                    $"Ett fel uppstod vid inloggning: {ex.Message}");
            }
        }
    }
}