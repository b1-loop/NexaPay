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
