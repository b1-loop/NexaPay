// ============================================================
// IdempotencyConflictException.cs – NexaPay.Domain/Exceptions
// ============================================================
// Kastas när två parallella requests med samma (Idempotency-Key,
// AccountId) försöker skapa en transaktion samtidigt och den ena
// vinner det unika indexet. Den förlorande request:en fångar
// detta undantag, slår upp vinnaren och returnerar den istället
// för att bubbla upp 500.
//
// Översätts från EF Core:s DbUpdateException i UnitOfWork så att
// Application-lagret slipper referera till EF Core direkt – samma
// pattern som ConcurrencyException.
// ============================================================

namespace NexaPay.Domain.Exceptions
{
    public class IdempotencyConflictException : Exception
    {
        public IdempotencyConflictException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
