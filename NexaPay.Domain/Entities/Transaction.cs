using NexaPay.Domain.Enums;
using NexaPay.Domain.ValueObjects;

namespace NexaPay.Domain.Entities
{
    public class Transaction : BaseEntity
    {
        public Money Amount { get; init; } = null!;
        public TransactionType Type { get; init; }
        public string Description { get; init; } = string.Empty;
        public Money BalanceAfterTransaction { get; init; } = null!;
        public Guid? ReceiverAccountId { get; init; }
        public Guid AccountId { get; init; }
        public Account? Account { get; set; }

        // Sätts endast för fakturabetalningar (Type = InvoicePayment):
        // bankgiro/plusgiro till den externa mottagaren och OCR-referensen.
        public string? Bankgiro { get; init; }
        public string? Ocr { get; init; }

        // Client-supplied UUID sent in Idempotency-Key header.
        // Unique (filtered) index ensures duplicate requests cannot
        // create duplicate transactions — the handler short-circuits on match.
        public Guid? IdempotencyKey { get; init; }
    }
}
