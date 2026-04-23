// ============================================================
// ValidationException.cs – NexaPay.Application/Common/Exceptions
// ============================================================
// Kastas av ValidationBehavior när ett Command/Query
// inte klarar FluentValidation-reglerna.
// ExceptionMiddleware fångar detta och returnerar HTTP 400.
//
// Innehåller en dictionary med alla valideringsfel:
//   Key   = fältnamnet (t.ex. "Amount")
//   Value = lista med felmeddelanden (t.ex. ["Måste vara större än 0"])
// ============================================================

using FluentValidation.Results; // ValidationFailure kommer från FluentValidation

namespace NexaPay.Application.Common.Exceptions
{
    public class ValidationException : Exception
    {
        // Dictionary som håller alla valideringsfel
        // Key   = namnet på fältet som failed (t.ex. "Amount", "AccountId")
        // Value = array med felmeddelanden för det fältet
        // T.ex. { "Amount": ["Måste vara större än 0", "Måste vara ett heltal"] }
        public IDictionary<string, string[]> Errors { get; }

        // Konstruktor som tar emot en lista med ValidationFailure från FluentValidation
        public ValidationException(IEnumerable<ValidationFailure> failures)
            : base("En eller flera valideringsfel uppstod.")
        {
            // Gruppera felen per fältnamn med LINQ
            // GroupBy grupperar alla fel med samma PropertyName tillsammans
            // ToDictionary omvandlar grupperingen till en dictionary
            Errors = failures
                .GroupBy(
                    // Gruppera på fältnamnet (t.ex. "Amount")
                    e => e.PropertyName,
                    // För varje fel i gruppen, ta felmeddelandet
                    e => e.ErrorMessage)
                .ToDictionary(
                    // Nyckeln i dictionary = fältnamnet
                    failureGroup => failureGroup.Key,
                    // Värdet = array med alla felmeddelanden för det fältet
                    failureGroup => failureGroup.ToArray());
        }
    }
}