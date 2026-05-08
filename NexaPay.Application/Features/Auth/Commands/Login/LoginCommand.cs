// ============================================================
// LoginCommand.cs
// NexaPay.Application/Features/Auth/Commands/Login
// ============================================================
// Command för att logga in en befintlig användare.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Auth.Commands.Login
{
    public record LoginCommand : IRequest<Result<AuthDto>>, ISensitiveRequest
    {
        // E-postadressen för användaren som loggar in
        public string Email { get; init; } = string.Empty;

        // Lösenordet som kontrolleras mot det hashade värdet
        // i databasen via Identity
        public string Password { get; init; } = string.Empty;

        // Förhindrar att lösenordet hamnar i klartext i loggarna.
        // LoggingBehavior loggar hela request-objektet – utan denna
        // override skulle Password synas i loggfiler.
        public override string ToString() =>
            $"LoginCommand {{ Email: {Email}, Password: [SKYDDAD] }}";
    }
}