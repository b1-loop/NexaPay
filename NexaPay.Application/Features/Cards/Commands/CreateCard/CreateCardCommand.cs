// ============================================================
// CreateCardCommand.cs
// NexaPay.Application/Features/Cards/Commands/CreateCard
// ============================================================
// Command för att skapa ett nytt bankkort kopplat till ett konto.
// Kortet skapas alltid med status Inactive – användaren
// aktiverar det separat (simulerat i verkligheten via brev/app).
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Cards.Commands.CreateCard
{
    public record CreateCardCommand : IRequest<Result<CardDto>>
    {
        // Vilket konto kortet ska kopplas till
        // Kontot måste tillhöra den inloggade användaren
        public Guid AccountId { get; init; }

        // Namnet som ska stå på kortet
        // T.ex. "ANNA SVENSSON" – brukar vara versaler på riktiga kort
        public string CardHolderName { get; init; } = string.Empty;

        // Den inloggade användarens ID – för behörighetskontroll
        // Vi kontrollerar att kontot faktiskt tillhör denna användare
        public string UserId { get; init; } = string.Empty;
    }
}