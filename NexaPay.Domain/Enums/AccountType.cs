// ============================================================
// AccountType.cs – NexaPay.Domain/Enums
// ============================================================
// En enum är en uppsättning namngivna konstanter.
// Istället för att lagra textsträngar i databasen lagrar vi
// heltal (0, 1, 2) vilket är snabbare och säkrare.
// C# konverterar automatiskt mellan enum och heltal via EF Core.
// ============================================================

namespace NexaPay.Domain.Enums
{
    public enum AccountType
    {
        // Lönekonto – används för dagliga transaktioner
        // Lagras som 0 i databasen
        Checking = 0,

        // Sparkonto – används för att spara pengar
        // Ofta med högre ränta men begränsade uttag
        // Lagras som 1 i databasen
        Savings = 1,

        // Investeringssparkonto – svenskt skattekonto för aktier
        // Lagras som 2 i databasen
        ISK = 2
    }
}