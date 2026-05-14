namespace NexaPay.Application.DTOs
{
    public class TransactionDto
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal BalanceAfterTransaction { get; set; }
        public Guid? ReceiverAccountId { get; set; }
        public Guid AccountId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Sätts bara för fakturabetalningar (Type = InvoicePayment).
        public string? Bankgiro { get; set; }
        public string? Ocr { get; set; }
    }
}
