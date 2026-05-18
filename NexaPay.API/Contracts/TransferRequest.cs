// ============================================================
// TransferRequest.cs – NexaPay.API/Contracts
// ============================================================
// HTTP-body för POST /api/transactions/transfer. Båda kontonas
// id krävs. Beloppet kan inte växlas mellan valutor – konton
// med olika valutor avvisas av handlern.
// ============================================================

namespace NexaPay.API.Contracts
{
    public record TransferRequest
    {
        public Guid FromAccountId { get; init; }
        public Guid ToAccountId { get; init; }
        public decimal Amount { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}
