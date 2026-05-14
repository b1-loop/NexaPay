// ============================================================
// TransactionType.cs – NexaPay.Domain/Enums
// ============================================================
// Beskriver vilken typ av transaktion som genomförts.
// Används för att filtrera transaktionshistorik och
// för att tillämpa rätt affärsregler i handlers.
// ============================================================

namespace NexaPay.Domain.Enums
{
    public enum TransactionType
    {
        // Insättning – pengar läggs till på kontot
        // Saldot ökar med beloppet
        // Lagras som 0 i databasen
        Deposit = 0,

        // Uttag – pengar tas från kontot
        // Saldot minskar med beloppet
        // Kräver att saldot är tillräckligt stort
        // Lagras som 1 i databasen
        Withdrawal = 1,

        // Överföring – pengar flyttas mellan två konton
        // Både ett uttag och en insättning sker atomärt
        // via Unit of Work – antingen båda eller ingen
        // Lagras som 2 i databasen
        Transfer = 2,

        // Fakturabetalning – pengar dras från kontot och betalas
        // till en extern mottagare (bankgiro/plusgiro) med OCR-referens.
        // Ingen intern mottagare finns – saldot minskar som vid ett uttag.
        // Lagras som 3 i databasen
        InvoicePayment = 3
    }
}