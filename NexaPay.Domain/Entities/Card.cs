// ============================================================
// Card.cs – NexaPay.Domain/Entities
// ============================================================
// Bankkort knutet till ett Account. Implementerar en tydlig
// tillståndsmaskin (Inactive → Active → Blocked / Expired) där
// varje övergång görs via en intention-revealing metod
// (Activate, Block, Unblock, MarkAsExpired). Försök att göra
// ogiltiga övergångar kastar InvalidOperationException så att
// domäninvarianterna alltid hålls.
//
// Säkerhet: vi sparar ALDRIG hela kortnumret eller CVV.
// Endast en opak token (CardToken) och de fyra sista siffrorna
// (Last4Digits) lagras – det räcker för att presentera kortet
// för användaren utan att exponera PAN.
// ============================================================

using NexaPay.Domain.Enums;
using NexaPay.Domain.Events;

namespace NexaPay.Domain.Entities
{
    public class Card : BaseEntity
    {
        // EF Core kräver en privat parameterlös konstruktor för att
        // kunna materialisera entiteten från databasen.
        private Card() { }

        // Tokeniserat kortnummer – ersätter PAN. Endast Domain kan
        // sätta värdet via fabriksmetoden Issue().
        public string CardToken { get; private set; } = string.Empty;

        // De fyra sista siffrorna – visas i UI för att kortet ska kunna
        // kännas igen, men exponerar inte hela kortnumret.
        public string Last4Digits { get; private set; } = string.Empty;

        // Kortinnehavarens namn (visas på kortet).
        public string CardHolderName { get; private set; } = string.Empty;

        // Endast år och månad har bankmässig betydelse, men vi lagrar en
        // exakt DateOnly för enkelhet (typiskt sista dagen i utgångsmånaden).
        public DateOnly ExpiryDate { get; private set; }

        // Status är privat-set: ändras endast via metoderna nedan så
        // ingen extern kod kan sätta ett ogiltigt tillstånd direkt.
        public CardStatus Status { get; private set; } = CardStatus.Inactive;

        // Främmande nyckel + navigationsproperty mot ägarkontot.
        public Guid AccountId { get; private set; }
        public Account? Account { get; set; }

        // Optimistic-concurrency-token: EF jämför vid SaveChanges och
        // kastar DbUpdateConcurrencyException om någon annan ändrat raden
        // sedan vi läste in den. Hindrar två samtidiga Block/Unblock
        // från att tyst skriva över varandra.
        public byte[] RowVersion { get; set; } = [];

        // ----------------------------------------------------
        // Fabriksmetod
        // ----------------------------------------------------

        // Skapar ett nytt kort i Inactive-tillstånd. Domänen har inget
        // ansvar för att generera PAN/CVV – det görs av Infrastructure
        // (CreateCardHandler) och passas in som färdiga värden.
        public static Card Issue(
            string cardToken,
            string last4Digits,
            string cardHolderName,
            DateOnly expiryDate,
            Guid accountId)
        {
            return new Card
            {
                Id = Guid.NewGuid(),
                CardToken = cardToken,
                Last4Digits = last4Digits,
                CardHolderName = cardHolderName,
                ExpiryDate = expiryDate,
                AccountId = accountId,
                Status = CardStatus.Inactive,
                CreatedAt = DateTime.UtcNow
            };
        }

        // ----------------------------------------------------
        // Tillståndsövergångar
        // ----------------------------------------------------

        // Aktiverar ett nytt eller åter-aktiverbart kort.
        // Tillåts endast från Inactive – inga andra övergångar.
        public void Activate()
        {
            if (Status == CardStatus.Active)
                throw new InvalidOperationException("Kortet är redan aktivt");
            if (Status == CardStatus.Blocked)
                throw new InvalidOperationException("Kan inte aktivera ett blockerat kort");
            if (Status == CardStatus.Expired)
                throw new InvalidOperationException("Kan inte aktivera ett utgånget kort");

            Status = CardStatus.Active;
            UpdatedAt = DateTime.UtcNow;
        }

        // Blockerar ett kort (t.ex. på kundens begäran om förlust).
        // Publicerar ett CardBlocked-event så att notifikations- och
        // audit-handlers reagerar.
        public void Block()
        {
            if (Status == CardStatus.Blocked)
                throw new InvalidOperationException("Kortet är redan blockerat");
            if (Status == CardStatus.Expired)
                throw new InvalidOperationException("Kan inte blockera ett utgånget kort");

            Status = CardStatus.Blocked;
            UpdatedAt = DateTime.UtcNow;

            RaiseDomainEvent(new CardBlocked(Id, AccountId, DateTime.UtcNow));
        }

        // Avblockerar ett kort och returnerar det till Active.
        // Tillåts endast från Blocked.
        public void Unblock()
        {
            if (Status != CardStatus.Blocked)
                throw new InvalidOperationException("Kortet är inte blockerat");

            Status = CardStatus.Active;
            UpdatedAt = DateTime.UtcNow;
        }

        // Markerar kortet som utgånget. Tänkt att anropas från ett
        // bakgrundsjobb som scannar kort vars ExpiryDate har passerat.
        // Tillåts från valfritt tillstånd eftersom utgång är en
        // tidsstyrd realitet, inte en användarinitierad åtgärd.
        public void MarkAsExpired()
        {
            Status = CardStatus.Expired;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
