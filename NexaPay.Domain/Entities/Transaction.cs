// ============================================================
// Transaction.cs – NexaPay.Domain/Entities
// ============================================================
// Representerar en finansiell händelse i NexaPay.
// Transaktioner är OFÖRÄNDERLIGA – de läggs bara till,
// aldrig ändras eller tas bort. Detta är standard i
// alla finansiella system för revisions- och säkerhetsskäl.
//
// Relationer:
//   - En Transaction tillhör ett Account (via AccountId)
// ============================================================

using NexaPay.Domain.Enums;

namespace NexaPay.Domain.Entities
{
    public class Transaction : BaseEntity // Ärver Id, CreatedAt, UpdatedAt
    {
        // --------------------------------------------------------
        // Transaktionsinformation
        // --------------------------------------------------------

        // Beloppet för transaktionen
        // Alltid positivt – typen (Deposit/Withdrawal) avgör riktningen
        // decimal används för exakta finansiella beräkningar
        public decimal Amount { get; set; }

        // Typ av transaktion – Deposit, Withdrawal eller Transfer
        // Avgör vilken affärslogik som tillämpas
        public TransactionType Type { get; set; }

        // En beskrivning av transaktionen
        // T.ex. "Insättning från Swedbank" eller "Överföring till sparkonto"
        public string Description { get; set; } = string.Empty;

        // Saldot på kontot EFTER att transaktionen genomförts
        // Detta lagras för att enkelt kunna visa saldohistorik
        // utan att behöva räkna om alla transaktioner varje gång
        public decimal BalanceAfterTransaction { get; set; }

        // --------------------------------------------------------
        // För överföringar – vilket konto fick pengarna?
        // --------------------------------------------------------

        // Om TransactionType är Transfer – vilket konto fick pengarna?
        // Null om det är en vanlig insättning eller uttag
        // "?" betyder att värdet är nullable
        public Guid? ReceiverAccountId { get; set; }

        // --------------------------------------------------------
        // Foreign Key – koppling till Account
        // --------------------------------------------------------

        // ID:t för det konto som transaktionen tillhör
        // T.ex. kontot som pengarna drogs ifrån
        public Guid AccountId { get; set; }

        // --------------------------------------------------------
        // Navigationsegenskaper
        // --------------------------------------------------------

        // Referens till kontot som äger denna transaktion
        // Fylls i av EF Core när vi använder .Include()
        public Account? Account { get; set; }
    }
}