// ============================================================
// WithdrawRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/transactions/withdraw. Idempotency-
// Key-header krävs på samma sätt som för insättning.
// ============================================================

namespace NexaPay.API.Contracts
{
    public record WithdrawRequest
    {
        public Guid AccountId { get; init; }
        public decimal Amount { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}
