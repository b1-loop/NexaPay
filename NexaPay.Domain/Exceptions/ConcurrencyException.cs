// ============================================================
// ConcurrencyException.cs – NexaPay.Domain/Exceptions
// ============================================================
// Domän-egen undantagstyp som kastas när ConcurrencyRetryBehavior
// gett upp efter sina försök. Wrapas runt EF Core:s
// DbUpdateConcurrencyException för att Application-lagret inte
// ska behöva referera till EF Core direkt.
// ============================================================

namespace NexaPay.Domain.Exceptions
{
    public class ConcurrencyException : Exception
    {
        public ConcurrencyException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
