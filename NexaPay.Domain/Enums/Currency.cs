// ============================================================
// Currency.cs – NexaPay.Domain/Enums
// ============================================================
// Valutor som NexaPay stöder. Ingår alltid tillsammans med ett
// belopp i Money-värdeobjektet så att vi aldrig blandar valutor
// i en aritmetisk operation.
// ============================================================

namespace NexaPay.Domain.Enums
{
    public enum Currency
    {
        SEK = 1,
        EUR = 2,
        USD = 3
    }
}
