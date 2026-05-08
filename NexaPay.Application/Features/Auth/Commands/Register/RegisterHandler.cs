using MediatR;
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

        public RegisterHandler(
            IAuthService authService,
            IAppSettings appSettings)
        {
            _authService = authService;
            _appSettings = appSettings;
        }

        public async Task<Result<AuthDto>> Handle(
            RegisterCommand request,
            CancellationToken cancellationToken)
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
    }
}
