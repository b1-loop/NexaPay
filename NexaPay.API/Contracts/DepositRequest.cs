// ============================================================
// DepositRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/transactions/deposit. Klienten ska
// dessutom skicka header 'Idempotency-Key: <uuid>' så att en
// dubbel POST inte skapar två insättningar.
// ============================================================

namespace NexaPay.API.Contracts
{
    public record DepositRequest
    {
        public Guid AccountId { get; init; }
        public decimal Amount { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}
