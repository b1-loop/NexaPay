// ============================================================
// FreezeAccountCommand.cs
// NexaPay.Application/Features/Accounts/Commands/FreezeAccount
// ============================================================
// MediatR-kommando för att frysa ett konto. Personal kan frysa
// vilket konto som helst (IsStaff=true), användare bara sina egna.
// Returnerar Result (utan värde) eftersom Freeze inte producerar ny data.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;

namespace NexaPay.Application.Features.Accounts.Commands.FreezeAccount
{
    public record FreezeAccountCommand : IRequest<Result>
    {
        public Guid AccountId { get; init; }
        public string UserId { get; init; } = string.Empty;
        public bool IsStaff { get; init; }
    }
}
