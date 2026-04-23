// ============================================================
// GetAccountByIdQuery.cs
// NexaPay.Application/Features/Accounts/Queries/GetAccountById
// ============================================================
// Hämtar ett specifikt konto baserat på ID.
// Kontrollerar även att användaren har rätt att se kontot
// (antingen Admin eller ägaren av kontot).
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Accounts.Queries.GetAccountById
{
    public record GetAccountByIdQuery : IRequest<Result<AccountDto>>
    {
        // ID:t för kontot vi vill hämta
        public Guid AccountId { get; init; }

        // ID:t för den inloggade användaren
        // Används för att kontrollera ägarskap
        public string UserId { get; init; } = string.Empty;

        // Om användaren är Admin – kan se alla konton
        public bool IsAdmin { get; init; }
    }
}