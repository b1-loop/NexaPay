// ============================================================
// GetTransactionsByAccountQuery.cs
// NexaPay.Application/Features/Transactions/Queries/
// GetTransactionsByAccount
// ============================================================
// Uppdaterad med pagineringsparametrar.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Transactions.Queries
    .GetTransactionsByAccount
{
    public record GetTransactionsByAccountQuery
        : IRequest<Result<PagedResult<TransactionDto>>>
    {
        // Vilket kontos transaktioner vi vill se
        public Guid AccountId { get; init; }

        // Den inloggade användarens ID
        public string UserId { get; init; } = string.Empty;

        // Om användaren är Admin/personal
        public bool IsAdmin { get; init; }

        // --------------------------------------------------------
        // Pagineringsparametrar
        // --------------------------------------------------------

        // Vilken sida vi vill hämta – börjar på 1
        // Default är 1 = första sidan
        public int Page { get; init; } = 1;

        // Antal transaktioner per sida
        // Default är 20 – rimligt för en kontoutdragsvy
        // Max är 100 – för att förhindra för stora requests
        public int PageSize { get; init; } = 20;
    }
}