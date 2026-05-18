// ============================================================
// PayInvoiceRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/transactions/pay-invoice. Bankgiro
// och OCR är obligatoriska – OCR valideras med mod-10/Luhn
// både i frontend och i Domain/OcrPolicy.
// ============================================================

namespace NexaPay.API.Contracts
{
    public record PayInvoiceRequest
    {
        public Guid AccountId { get; init; }
        public decimal Amount { get; init; }
        public string Bankgiro { get; init; } = string.Empty;
        public string Ocr { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }
}
