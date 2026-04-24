// ============================================================
// DepositCommand.cs
// NexaPay.Application/Features/Transactions/Commands/Deposit
// ============================================================
// Command för att sätta in pengar på ett konto.
// Insättning = saldot ökar med beloppet.
// En transaktion av typen Deposit skapas automatiskt.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Transactions.Commands.Deposit
{
    public record DepositCommand : IRequest<Result<TransactionDto>>
    {
        // Vilket konto pengarna ska sättas in på
        public Guid AccountId { get; init; }

        // Beloppet som ska sättas in – måste vara > 0
        // Valideras av DepositValidator innan handleren körs
        public decimal Amount { get; init; }

        // Beskrivning av insättningen – syns i kontoutdraget
        // T.ex. "Insättning från Swedbank" eller "Lön april"
        public string Description { get; init; } = string.Empty;

        // Den inloggade användarens ID – för behörighetskontroll
        public string UserId { get; init; } = string.Empty;
    }
}