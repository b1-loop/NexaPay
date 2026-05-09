using MediatR;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.Common.Models;
using NexaPay.Application.Common.Policies;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Auth.Commands.Register
{
    public class RegisterHandler
        : IRequestHandler<RegisterCommand, Result<AuthDto>>
    {
        private readonly IAuthService _authService;
        private readonly IStaffEmailPolicy _staffEmailPolicy;

        public RegisterHandler(
            IAuthService authService,
            IStaffEmailPolicy staffEmailPolicy)
        {
            _authService = authService;
            _staffEmailPolicy = staffEmailPolicy;
        }

        public async Task<Result<AuthDto>> Handle(
            RegisterCommand request,
            CancellationToken cancellationToken)
        {
            var error = _staffEmailPolicy.Validate(request.Email, request.Role);
            if (error != null)
                return Result<AuthDto>.Failure(error);

            return await _authService.RegisterAsync(
                request.Email,
                request.Password,
                request.Role);
        }
    }
}
