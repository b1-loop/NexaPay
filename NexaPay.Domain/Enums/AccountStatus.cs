// ============================================================
// AccountStatus.cs – NexaPay.Domain/Enums
// ============================================================
// Beskriver i vilket tillstånd ett konto befinner sig.
// Domänregler:
//   * Open   – normal drift, alla operationer tillåtna.
//   * Frozen – temporärt spärrat av bankpersonal (t.ex. vid
//              misstänkt bedrägeri). Inga in-/uttag tillåtna.
//   * Closed – permanent avslutat. Saldo måste vara 0 innan
//              stängning. Kan inte återöppnas.
// Lagras som heltal (0/1/2) i databasen.
// ============================================================

namespace NexaPay.Domain.Enums
{
    public enum AccountStatus
    {
        Open = 0,
        Frozen = 1,
        Closed = 2
    }
}
