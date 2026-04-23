// ============================================================
// Account.cs – NexaPay.Domain/Entities
// ============================================================
// Representerar ett bankkonto i NexaPay.
// Ärver från BaseEntity vilket ger oss Id, CreatedAt, UpdatedAt.
//
// Relationer:
//   - En Account tillhör en användare (via OwnerId)
//   - En Account kan ha många Transactions
//   - En Account kan ha många Cards
// ============================================================

using NexaPay.Domain.Enums;
using NexaPay.Domain.Entities;
using System.Transactions; // Vi importerar våra egna enums

namespace NexaPay.Domain.Entities
{
    public class Account : BaseEntity // Ärver Id, CreatedAt, UpdatedAt
    {
        // --------------------------------------------------------
        // Grundläggande information om kontot
        // --------------------------------------------------------

        // Kontonummer – ett läsbart nummer för användaren
        // T.ex. "SE1234567890" – genereras när kontot skapas
        public string AccountNumber { get; set; } = string.Empty;

        // Kontonamn – ett smeknamn som användaren ger kontot
        // T.ex. "Mitt sparkonto" eller "Hushållskassan"
        public string AccountName { get; set; } = string.Empty;

        // Aktuellt saldo på kontot
        // "decimal" används för pengar – aldrig "double" eller "float"
        // eftersom de kan ge avrundningsfel (t.ex. 0.1 + 0.2 = 0.30000000000000004)
        // decimal är exakt och perfekt för finansiella beräkningar
        public decimal Balance { get; set; }

        // Typ av konto – Checking, Savings eller ISK
        // Lagras som ett heltal i databasen men visas som text i C#
        public AccountType AccountType { get; set; }

        // Om kontot är aktivt eller stängt
        // false = stängt konto, true = aktivt konto
        public bool IsActive { get; set; } = true; // Nytt konto är alltid aktivt

        // --------------------------------------------------------
        // Koppling till användaren (Foreign Key)
        // --------------------------------------------------------

        // ID:t för användaren som äger detta konto
        // "string" eftersom ASP.NET Identity använder string som användar-ID
        // Detta är en Foreign Key – pekar på användartabellen
        public string OwnerId { get; set; } = string.Empty;

        // --------------------------------------------------------
        // Navigationsegenskaper (Relations)
        // --------------------------------------------------------
        // Navigationsegenskaper används av Entity Framework för att
        // ladda relaterad data. De lagras INTE i Account-tabellen –
        // de är bara C#-objekt som EF fyller i när vi inkluderar dem.
        // Vi initierar dem som tomma listor för att undvika null-fel.

        // Alla transaktioner som tillhör detta konto
        // En Account kan ha 0 till många Transactions
        public ICollection<Transaction> Transactions { get; set; }
            = new List<Transaction>();

        // Alla kort som är kopplade till detta konto
        // En Account kan ha 0 till många Cards
        public ICollection<Card> Cards { get; set; }
            = new List<Card>();
    }
}