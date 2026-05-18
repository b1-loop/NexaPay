// ============================================================
// Transaction.cs – NexaPay.Domain/Entities
// ============================================================
// Oföränderlig logg-post över en finansiell händelse mot ett
// konto: insättning, uttag, överföring eller fakturabetalning.
//
// Alla properties är 'init' (utom Account-navigationen som EF
// fyller i). Det innebär att en transaktion aldrig kan ändras
// efter att den skapats – endast nya rader läggs till. Detta
// uppfyller bankregler om revisionsbarhet (audit trail).
//
// Skapas alltid av Account-aggregatet (Deposit/Withdraw/Transfer/
// PayInvoice) så att saldo och händelse skapas atomärt i samma
// SaveChanges-anrop.
// ============================================================

using NexaPay.Domain.Enums;
using NexaPay.Domain.ValueObjects;

namespace NexaPay.Domain.Entities
{
    public class Transaction : BaseEntity
    {
        // Beloppet som rörde sig. Lagras som value object (Money)
        // för att alltid bära med sig sin valuta.
        public Money Amount { get; init; } = null!;

        // Typ av transaktion – Deposit, Withdrawal, Transfer eller InvoicePayment.
        public TransactionType Type { get; init; }

        // Fritext från användaren ("Hyra mars", "Lunch", osv.).
        public string Description { get; init; } = string.Empty;

        // Saldo direkt EFTER att transaktionen utförts. Sparas ihop
        // med transaktionen så att vi snabbt kan visa kontohistorik
        // utan att räkna om saldot för varje rad.
        public Money BalanceAfterTransaction { get; init; } = null!;

        // Sätts endast för Transfer – id på det mottagande kontot.
        public Guid? ReceiverAccountId { get; init; }

        // Främmande nyckel + navigation till ägarkontot (avsändarsidan
        // för uttag/överföring, mottagarsidan för inkommande pengar).
        public Guid AccountId { get; init; }
        public Account? Account { get; set; }

        // Sätts endast för fakturabetalningar (Type = InvoicePayment):
        // bankgiro/plusgiro till den externa mottagaren och OCR-referensen.
        public string? Bankgiro { get; init; }
        public string? Ocr { get; init; }

        // Idempotency-Key från klienten (header eller body). Tillsammans
        // med ett filtrerat unikt index i SQL (där IdempotencyKey != NULL)
        // garanterar detta att en klient som råkar göra om samma
        // POST-anrop inte skapar dubbla transaktioner – handlern
        // kortsluter och returnerar det första resultatet istället.
        public Guid? IdempotencyKey { get; init; }
    }
}
