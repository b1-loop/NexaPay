// ============================================================
// ActivateCardCommand.cs
// NexaPay.Application/Features/Cards/Commands/ActivateCard
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;

namespace NexaPay.Application.Features.Cards.Commands.ActivateCard
{
    public record ActivateCardCommand : IRequest<Result>
    {
        public Guid CardId { get; init; }
        public string UserId { get; init; } = string.Empty;
        public bool IsStaff { get; init; }
    }
}
