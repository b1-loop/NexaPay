// ============================================================
// BaseEntity.cs – NexaPay.Domain/Entities
// ============================================================
// En abstrakt basklass som alla entiteter ärver från.
// "Abstrakt" betyder att man inte kan skapa ett objekt
// direkt av BaseEntity – det är bara en mall.
//
// Genom arv får Account, Card och Transaction automatiskt
// dessa properties utan att vi behöver skriva dem igen.
// ============================================================

namespace NexaPay.Domain.Entities
{
    // "abstract" – kan inte instansieras direkt
    // Används bara som bas för andra klasser
    public abstract class BaseEntity
    {
        // Unikt ID för varje rad i databasen
        // Guid är ett globalt unikt ID (t.ex. "3f2504e0-4f89-11d3-9a0c-0305e82c3301")
        // Vi använder Guid istället för int för att:
        // 1. Det är säkrare – ingen kan gissa nästa ID
        // 2. Det fungerar bra i distribuerade system
        // 3. Det är standard i moderna API:er
        public Guid Id { get; set; }

        // Tidsstämpel för när raden skapades i databasen
        // Sätts automatiskt i Repository när vi skapar ett objekt
        // "DateTime" lagrar både datum och tid
        public DateTime CreatedAt { get; set; }

        // Tidsstämpel för senaste uppdatering
        // Är null om raden aldrig uppdaterats efter skapandet
        // "?" efter DateTime betyder att värdet kan vara null (nullable)
        public DateTime? UpdatedAt { get; set; }
    }
}