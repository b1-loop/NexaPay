using MediatR;
using NexaPay.Application.Common.Models;

namespace NexaPay.Application.Features.Cards.Commands.UnblockCard
{
    public record UnblockCardCommand : IRequest<Result>
    {
        public Guid CardId { get; init; }
        public string AdminId { get; init; } = string.Empty;
    }
}
