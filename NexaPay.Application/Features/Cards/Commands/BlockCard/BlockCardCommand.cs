// ============================================================
// BlockCardCommand.cs
// NexaPay.Application/Features/Cards/Commands/BlockCard
// ============================================================
// Command för att blockera ett bankkort.
// Bara Admin kan blockera ett kort.
// När ett kort är blockerat kan inga transaktioner göras med det.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;

namespace NexaPay.Application.Features.Cards.Commands.BlockCard
{
    // Returnerar Result utan data – vi behöver bara veta om det lyckades
    public record BlockCardCommand : IRequest<Result>
    {
        // ID:t för kortet som ska blockeras
        public Guid CardId { get; init; }

        // Anledning till blockeringen – sparas för revisionsspår
        // T.ex. "Misstänkt bedrägeri" eller "Förlorat kort"
        public string Reason { get; init; } = string.Empty;

        // Den inloggade adminens ID – för loggning
        public string AdminId { get; init; } = string.Empty;
    }
}