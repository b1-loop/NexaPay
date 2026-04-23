// ============================================================
// NotFoundException.cs – NexaPay.Application/Common/Exceptions
// ============================================================
// Kastas när en efterfrågad resurs inte hittas i databasen.
// ExceptionMiddleware i API-lagret fångar detta och
// returnerar automatiskt HTTP 404 Not Found till klienten.
//
// Exempel på användning i en handler:
//   var account = await _uow.Accounts.GetByIdAsync(id);
//   if (account == null)
//       throw new NotFoundException(nameof(Account), id);
// ============================================================

namespace NexaPay.Application.Common.Exceptions
{
    // Ärver från Exception – det är så man skapar egna exception-klasser i C#
    public class NotFoundException : Exception
    {
        // Konstruktor som tar emot resursnamnet och ID:t som inte hittades
        // "name" = vilken typ av resurs (t.ex. "Account", "Card")
        // "key"  = vilket ID vi letade efter (t.ex. en Guid)
        public NotFoundException(string name, object key)
            // base() anropar Exception-basklassens konstruktor med ett meddelande
            // string interpolation skapar ett beskrivande felmeddelande
            // T.ex. "Entity 'Account' (3f2504e0-...) was not found."
            : base($"Entity '{name}' ({key}) was not found.")
        {
        }

        // Alternativ konstruktor med ett eget meddelande
        // Används när vi vill ha ett mer beskrivande felmeddelande
        // T.ex. new NotFoundException("Kontot med nummer SE123 hittades inte")
        public NotFoundException(string message)
            : base(message)
        {
        }
    }
}