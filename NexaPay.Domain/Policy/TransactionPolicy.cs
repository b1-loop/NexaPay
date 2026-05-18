// ============================================================
// TransactionPolicy.cs – NexaPay.Domain/Policy
// ============================================================
// Centraliserade affärsregler för transaktioner. Genom att samla
// gränsvärdena på ETT ställe slipper handlers och validators
// duplicera magiska tal.
// ============================================================

namespace NexaPay.Domain.Policy
{
    public static class TransactionPolicy
    {
        // Maxbelopp per enskild transaktion (deposit, withdraw, transfer).
        // Drivet av AML (penningtvättsdirektivet) som kräver särskild
        // hantering av stora kontantbelopp.
        public const decimal MaxTransactionAmount = 1_000_000m;

        // Maxlängd på fritextbeskrivning – matchar databaskolumnen.
        public const int MaxDescriptionLength = 500;
    }
}
