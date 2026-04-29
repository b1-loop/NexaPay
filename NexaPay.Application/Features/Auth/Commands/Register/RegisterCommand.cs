// ============================================================
// RegisterCommand.cs
// NexaPay.Application/Features/Auth/Commands/Register
// ============================================================
// Command för att registrera en ny användare.
// Tar nu en roll-sträng istället för bool isAdmin.
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

        // Lösenordet – hashas av Identity i AuthService
        public string Password { get; init; } = string.Empty;

        // Rollen för den nya användaren
        // Måste vara en av: Admin, BankManager, Teller, Auditor, User
        // Valideras av RegisterValidator och IsValidRole i AuthService
        public string Role { get; init; } = "User";
        // Standard är User – den säkraste standardrollen
    }
}