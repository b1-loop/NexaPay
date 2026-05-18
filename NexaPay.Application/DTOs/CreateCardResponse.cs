// ============================================================
// CreateCardResponse.cs – NexaPay.Application/DTOs
// ============================================================
// Specialfall vid kort-skapning: detta är ENDA tillfället då vi
// returnerar hela kortnumret (PAN) och CVV till klienten – de
// lagras aldrig i databasen, så efter denna respons är de bara
// tillgängliga om klienten antecknat dem. Alla framtida anrop
// returnerar CardDto med maskerat nummer.
// ============================================================

namespace NexaPay.Application.DTOs
{
    public class CreateCardResponse
    {
        public CardDto Card { get; set; } = null!;

        // Hela PAN – returneras EN gång vid skapning, lagras aldrig.
        public string CardNumber { get; set; } = string.Empty;

        // CVV – returneras EN gång vid skapning, lagras aldrig.
        public string Cvv { get; set; } = string.Empty;
    }
}
