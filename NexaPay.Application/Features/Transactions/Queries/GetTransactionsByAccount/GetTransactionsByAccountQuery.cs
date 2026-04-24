// ============================================================
// GetTransactionsByAccountQuery.cs
// NexaPay.Application/Features/Transactions/Queries/GetTransactionsByAccount
// ============================================================
// Hämtar transaktionshistoriken för ett specifikt konto.
// Används för att visa kontoutdrag i applikationen.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Transactions.Queries.GetTransactionsByAccount
{
    public record GetTransactionsByAccountQuery
        : IRequest<Result<IEnumerable<TransactionDto>>>
    {
        // Vilket kontos transaktioner vi vill se
        public Guid AccountId { get; init; }

        // Den inloggade användarens ID – för behörighetskontroll
        public string UserId { get; init; } = string.Empty;

        // Om användaren är Admin – kan se alla kontons transaktioner
        public bool IsAdmin { get; init; }
    }
}
