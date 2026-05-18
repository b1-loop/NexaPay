// ============================================================
// CreateCardRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/cards. Svaret är CreateCardResponse
// – enda tillfället då hela kortnumret + CVV exponeras.
// ============================================================

namespace NexaPay.API.Contracts
{
    public record CreateCardRequest
    {
        public Guid AccountId { get; init; }
        public string CardHolderName { get; init; } = string.Empty;
    }
}
