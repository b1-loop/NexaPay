// ============================================================
// TransferCommand.cs
// NexaPay.Application/Features/Transactions/Commands/Transfer
// ============================================================
// Command för att överföra pengar mellan två konton.
// Detta är den mest komplexa operationen i NexaPay.
//
// Unit of Work är KRITISKT här:
//   - Konto A tappar pengar (Withdrawal)
//   - Konto B får pengar (Deposit)
//   - Två transaktionsposter skapas
//   - Alla fyra operationer sparas ATOMÄRT
//   - Om något misslyckas → rullar allt tillbaka
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Transactions.Commands.Transfer
{
    public record TransferCommand : IRequest<Result<TransactionDto>>
    {
        // Kontot som pengarna dras ifrån
        public Guid FromAccountId { get; init; }

        // Kontot som pengarna sätts in på
        public Guid ToAccountId { get; init; }

        // Beloppet som ska överföras
        public decimal Amount { get; init; }

        // Beskrivning av överföringen
        public string Description { get; init; } = string.Empty;

        // Den inloggade användarens ID
        // Måste äga FromAccount – man kan inte överföra från andras konton
        public string UserId { get; init; } = string.Empty;
    }
}