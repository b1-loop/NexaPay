// ============================================================
// RegisterCommand.cs
// NexaPay.Application/Features/Auth/Commands/Register
// ============================================================
// Command för att registrera en ny användare.
// Skickas från AuthController via MediatR.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Auth.Commands.Register
{
    public record RegisterCommand : IRequest<Result<AuthDto>>
    {
        // E-postadressen för den nya användaren
        public string Email { get; init; } = string.Empty;

        // Lösenordet – valideras av RegisterValidator
        // Hashas av Identity i AuthService – aldrig lagrat i klartext
        public string Password { get; init; } = string.Empty;

        // Om true skapas användaren med Admin-rollen
        // Om false skapas användaren med User-rollen
        public bool IsAdmin { get; init; } = false;
    }
}