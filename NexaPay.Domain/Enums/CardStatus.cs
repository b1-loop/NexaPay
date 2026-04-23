// ============================================================
// CardStatus.cs – NexaPay.Domain/Enums
// ============================================================
// Beskriver vilket tillstånd ett kort befinner sig i.
// Affärsregeln är: transaktioner får bara genomföras
// om kortets status är Active.
// ============================================================

namespace NexaPay.Domain.Enums
{
    public enum CardStatus
    {
        // Kortet är aktivt och kan användas för transaktioner
        // Lagras som 0 i databasen
        Active = 0,

        // Kortet är blockerat – t.ex. vid misstänkt bedrägeri
        // Kan blockeras av Admin via API
        // Lagras som 1 i databasen
        Blocked = 1,

        // Kortet har passerat sitt utgångsdatum
        // Sätts automatiskt när ExpiryDate passerats
        // Lagras som 2 i databasen
        Expired = 2,

        // Kortet är skapat men ännu inte aktiverat av användaren
        // Lagras som 3 i databasen
        Inactive = 3
    }
}