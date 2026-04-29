// ============================================================
// RegisterHandler.cs
// NexaPay.Application/Features/Auth/Commands/Register
// ============================================================
// Hanterar RegisterCommand.
//
// Notera: Handleren känner bara till IAuthService
// Den vet INGENTING om UserManager, Identity eller databaser
// Det är precis vad Clean Architecture kräver!
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
        // IAuthService – Application känner bara till interfacet
        // Infrastructure implementerar det med UserManager
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
                // Delegera till AuthService som hanterar
                // all Identity-logik i Infrastructure
                return await _authService.RegisterAsync(
                    request.Email,
                    request.Password,
                    request.IsAdmin);
            }
            catch (Exception ex)
            {
                return Result<AuthDto>.Failure(
                    $"Ett fel uppstod vid registrering: {ex.Message}");
            }
        }
    }
}