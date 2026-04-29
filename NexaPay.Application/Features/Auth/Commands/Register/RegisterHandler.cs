// ============================================================
// RegisterHandler.cs
// NexaPay.Application/Features/Auth/Commands/Register
// ============================================================

using MediatR;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Auth.Commands.Register
{
    public class RegisterHandler
        : IRequestHandler<RegisterCommand, Result<AuthDto>>
    {
        private readonly IAuthService _authService;

        public RegisterHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<Result<AuthDto>> Handle(
            RegisterCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Delegera till AuthService med den angivna rollen
                return await _authService.RegisterAsync(
                    request.Email,
                    request.Password,
                    request.Role); // Skickar roll istället för bool
            }
            catch (Exception ex)
            {
                return Result<AuthDto>.Failure(
                    $"Ett fel uppstod vid registrering: {ex.Message}");
            }
        }
    }
}