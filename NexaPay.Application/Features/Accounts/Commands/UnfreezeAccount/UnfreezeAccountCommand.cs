// ============================================================
// UnfreezeAccountCommand.cs
// NexaPay.Application/Features/Accounts/Commands/UnfreezeAccount
// ============================================================
// MediatR-kommando för att avfrysa ett konto. Spegelbild av
// FreezeAccountCommand – samma behörighetsmodell.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;

namespace NexaPay.Application.Features.Accounts.Commands.UnfreezeAccount
{
    public record UnfreezeAccountCommand : IRequest<Result>
    {
        public Guid AccountId { get; init; }
        public string UserId { get; init; } = string.Empty;
        public bool IsStaff { get; init; }
    }
}
