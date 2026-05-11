using NexaPay.Domain.Enums;
using NexaPay.Domain.Events;

namespace NexaPay.Domain.Entities
{
    public class Card : BaseEntity
    {
        public string CardToken { get; set; } = string.Empty;
        public string Last4Digits { get; set; } = string.Empty;
        public string CardHolderName { get; set; } = string.Empty;
        public DateOnly ExpiryDate { get; set; }
        public CardStatus Status { get; private set; } = CardStatus.Inactive;
        public Guid AccountId { get; set; }
        public Account? Account { get; set; }

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

        public void Unblock()
        {
            if (Status != CardStatus.Blocked)
                throw new InvalidOperationException("Kortet är inte blockerat");

            Status = CardStatus.Active;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkAsExpired()
        {
            Status = CardStatus.Expired;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
