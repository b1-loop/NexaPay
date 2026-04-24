// ============================================================
// GetCardsByAccountQuery.cs
// NexaPay.Application/Features/Cards/Queries/GetCardsByAccount
// ============================================================
// Hämtar alla kort kopplade till ett specifikt konto.
// Kontrollerar att användaren har rätt att se kontots kort.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Cards.Queries.GetCardsByAccount
{
    public record GetCardsByAccountQuery : IRequest<Result<IEnumerable<CardDto>>>
    {
        // Vilket kontos kort vi vill hämta
        public Guid AccountId { get; init; }

        // Den inloggade användarens ID – för behörighetskontroll
        public string UserId { get; init; } = string.Empty;

        // Om användaren är Admin – kan se alla kontons kort
        public bool IsAdmin { get; init; }
    }
}