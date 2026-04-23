// ============================================================
// ICardRepository.cs – NexaPay.Domain/Interfaces
// ============================================================
// Specifikt interface för Card-operationer.
// Ärver från IRepository<Card> för grundläggande CRUD.
//
// Kortspecifika operationer:
//   - Hämta alla kort för ett konto
//   - Hämta kort via kortnummer
// ============================================================

using NexaPay.Domain.Entities;

namespace NexaPay.Domain.Interfaces
{
    public interface ICardRepository : IRepository<Card>
    {
        // Hämta alla kort kopplade till ett specifikt konto
        // En användare kan ha flera kort på samma konto
        // T.ex. ett fysiskt kort och ett virtuellt kort
        Task<IEnumerable<Card>> GetCardsByAccountIdAsync(Guid accountId);

        // Hämta ett specifikt kort via kortnummer
        // Kortnummer är unikt – som ett ID för kortet
        // Returnerar null om kortnumret inte finns
        Task<Card?> GetByCardNumberAsync(string cardNumber);
    }
}