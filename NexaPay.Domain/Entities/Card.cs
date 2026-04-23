// ============================================================
// Card.cs – NexaPay.Domain/Entities
// ============================================================
// Representerar ett bankkort kopplat till ett Account.
// Ett kort kan vara aktivt, blockerat, utgånget eller inaktivt.
//
// Relationer:
//   - Ett Card tillhör ett Account (via AccountId)
// ============================================================

using NexaPay.Domain.Enums;

namespace NexaPay.Domain.Entities
{
    public class Card : BaseEntity // Ärver Id, CreatedAt, UpdatedAt
    {
        // --------------------------------------------------------
        // Kortinformation
        // --------------------------------------------------------

        // Kortnummer – 16 siffror som på ett riktigt bankkort
        // T.ex. "4532 1234 5678 9010"
        // I verkligheten skulle detta vara krypterat i databasen
        public string CardNumber { get; set; } = string.Empty;

        // Kortinnehavarens namn – visas på kortet
        // T.ex. "ANNA SVENSSON"
        public string CardHolderName { get; set; } = string.Empty;

        // Utgångsdatum för kortet
        // När detta datum passeras ska status sättas till Expired
        // Vi lagrar bara datum (inte tid) via DateOnly
        public DateOnly ExpiryDate { get; set; }

        // CVV – tresiffrigt säkerhetsnummer på baksidan
        // I verkligheten lagras aldrig CVV i databasen (PCI-DSS krav)
        // Vi gör det här för enkelhetens skull i utbildningssyfte
        public string CVV { get; set; } = string.Empty;

        // Kortets nuvarande status – Active, Blocked, Expired, Inactive
        // Nytt kort börjar alltid som Inactive tills användaren aktiverar det
        public CardStatus Status { get; set; } = CardStatus.Inactive;

        // --------------------------------------------------------
        // Foreign Key – koppling till Account
        // --------------------------------------------------------

        // ID:t för det konto som detta kort tillhör
        // EF Core använder denna för att skapa en Foreign Key i databasen
        // Alla transaktioner med detta kort belastar det kopplade kontot
        public Guid AccountId { get; set; }

        // --------------------------------------------------------
        // Navigationsegenskaper
        // --------------------------------------------------------

        // Referens till det Account som äger detta kort
        // EF Core fyller i detta objekt när vi använder .Include(c => c.Account)
        // "?" betyder att det kan vara null om vi inte laddar det explicit
        public Account? Account { get; set; }
    }
}