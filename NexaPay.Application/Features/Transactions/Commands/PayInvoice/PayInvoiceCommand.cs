// ============================================================
// PayInvoiceCommand.cs
// NexaPay.Application/Features/Transactions/Commands/PayInvoice
// ============================================================
// Command för att betala en faktura från ett konto.
// Pengarna dras från kontot (som ett uttag) och betalas till en
// extern mottagare (bankgiro/plusgiro) med en OCR-referens.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Transactions.Commands.PayInvoice
{
    public record PayInvoiceCommand : IRequest<Result<TransactionDto>>
    {
        // Kontot som fakturan betalas från
        public Guid AccountId { get; init; }

        // Beloppet som ska betalas – måste vara > 0 och <= saldo
        public decimal Amount { get; init; }

        // Mottagarens bankgiro/plusgiro
        public string Bankgiro { get; init; } = string.Empty;

        // OCR-referensnummer – valideras med mod-10 (Luhn)
        public string Ocr { get; init; } = string.Empty;

        // Beskrivning – syns i kontoutdraget
        public string Description { get; init; } = string.Empty;

        // Den inloggade användarens ID
        public string UserId { get; init; } = string.Empty;

        // Om den inloggade användaren är personal
        // Personal kan betala fakturor från kunders konton
        public bool IsStaff { get; init; }

        // Client-supplied UUID (Idempotency-Key header). Null = no deduplication.
        public Guid? IdempotencyKey { get; init; }
    }
}
