// ============================================================
// WithdrawCommand.cs
// NexaPay.Application/Features/Transactions/Commands/Withdraw
// ============================================================
// Command för att ta ut pengar från ett konto.
// Affärsregel: Saldot måste räcka – vi tillåter inte negativt saldo.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Transactions.Commands.Withdraw
{
    public record WithdrawCommand : IRequest<Result<TransactionDto>>
    {
        // Vilket konto pengarna ska tas från
        public Guid AccountId { get; init; }

        // Beloppet som ska tas ut – måste vara > 0 och <= saldo
        public decimal Amount { get; init; }

        // Beskrivning av uttaget – syns i kontoutdraget
        public string Description { get; init; } = string.Empty;

        // Den inloggade användarens ID
        public string UserId { get; init; } = string.Empty;

        // Om den inloggade användaren är personal
        // Personal kan göra uttag från kunders konton
        public bool IsStaff { get; init; }

        // Client-supplied UUID (Idempotency-Key header). Null = no deduplication.
        public Guid? IdempotencyKey { get; init; }
    }
}