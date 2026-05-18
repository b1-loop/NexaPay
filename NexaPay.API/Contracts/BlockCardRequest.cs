// ============================================================
// BlockCardRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/cards/{id}/block. Reason loggas i
// audit-spåret men påverkar inte själva blockeringen.
// ============================================================

namespace NexaPay.API.Contracts
{
    public record BlockCardRequest
    {
        public string Reason { get; init; } = string.Empty;
    }
}
