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
