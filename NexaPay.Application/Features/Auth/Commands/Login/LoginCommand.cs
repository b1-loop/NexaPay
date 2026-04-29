// ============================================================
// LoginCommand.cs
// NexaPay.Application/Features/Auth/Commands/Login
// ============================================================
// Command för att logga in en befintlig användare.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Auth.Commands.Login
{
    public record LoginCommand : IRequest<Result<AuthDto>>
    {
        // E-postadressen för användaren som loggar in
        public string Email { get; init; } = string.Empty;

        // Lösenordet som kontrolleras mot det hashade värdet
        // i databasen via Identity
        public string Password { get; init; } = string.Empty;
    }
}