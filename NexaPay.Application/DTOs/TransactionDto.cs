// ============================================================
// TransactionDto.cs – NexaPay.Application/DTOs
// ============================================================
// Data Transfer Object för Transaction.
// Representerar en transaktion i kontoutdraget.
//
// En transaktion är oföränderlig – den visar exakt vad
// som hände vid ett specifikt tillfälle.
// ============================================================

namespace NexaPay.Application.DTOs
{
    public class TransactionDto
    {
        // Transaktionens unika ID
        public Guid Id { get; set; }

        // Beloppet för transaktionen – alltid positivt
        public decimal Amount { get; set; }

        // Transaktionstyp som text – "Deposit", "Withdrawal", "Transfer"
        public string Type { get; set; } = string.Empty;

        // Beskrivning av transaktionen
        // T.ex. "Insättning" eller "Överföring till sparkonto"
        public string Description { get; set; } = string.Empty;

        // Saldot efter att transaktionen genomfördes
        // Användbart för att visa saldohistorik i kontoutdrag
        public decimal BalanceAfterTransaction { get; set; }

        // Om det är en överföring – vilket konto fick pengarna?
        // Null för insättningar och uttag
        public Guid? ReceiverAccountId { get; set; }

        // Vilket konto transaktionen tillhör
        public Guid AccountId { get; set; }

        // När transaktionen genomfördes – viktigt för kontoutdrag
        public DateTime CreatedAt { get; set; }
    }
}