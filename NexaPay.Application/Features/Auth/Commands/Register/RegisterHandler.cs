using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Auth.Commands.Register
{
    public class RegisterHandler
        : IRequestHandler<RegisterCommand, Result<AuthDto>>
    {
        private readonly IAuthService _authService;
        private readonly IAppSettings _appSettings;
        private readonly ILogger<RegisterHandler> _logger;

        public RegisterHandler(
            IAuthService authService,
            IAppSettings appSettings,
            ILogger<RegisterHandler> logger)
        {
            _authService = authService;
            _appSettings = appSettings;
            _logger = logger;
        }

        public async Task<Result<AuthDto>> Handle(
            RegisterCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                var isStaffRole = request.Role != Roles.User;
                var isStaffEmail = request.Email.EndsWith(
                    $"@{_appSettings.StaffDomain}",
                    StringComparison.OrdinalIgnoreCase);

                if (isStaffRole && !isStaffEmail)
                    return Result<AuthDto>.Failure(
                        $"Personalroller kräver en @{_appSettings.StaffDomain}-e-postadress");

                return await _authService.RegisterAsync(
                    request.Email,
                    request.Password,
                    request.Role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oväntat fel vid registrering för e-post {Email}", request.Email);
                return Result<AuthDto>.Failure(
                    "Ett oväntat fel uppstod. Försök igen senare.");
            }
        }
    }
}
