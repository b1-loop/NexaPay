// ============================================================
// AccountDto.cs – NexaPay.Application/DTOs
// ============================================================
// Data Transfer Object för Account.
// Detta är vad klienten (frontend/mobilapp) ser när de
// anropar API:et – inte den rå Account-entiteten.
//
// Vi väljer medvetet att INTE inkludera:
//   - OwnerId (intern implementation)
//   - Navigationsegenskaper direkt (undviker cirkulära refs)
// ============================================================

namespace NexaPay.Application.DTOs
{
    public class AccountDto
    {
        // Kontots unika ID – klienten behöver detta för att
        // referera till kontot i framtida anrop
        public Guid Id { get; set; }

        // Kontonummer – det läsbara numret (t.ex. "SE1234567890")
        public string AccountNumber { get; set; } = string.Empty;

        // Kontonamn – användarens smeknamn för kontot
        public string AccountName { get; set; } = string.Empty;

        // Aktuellt saldo – decimal för exakta finansiella beräkningar
        public decimal Balance { get; set; }

        // Kontotyp som text istället för enum-nummer
        // Klienten ser "Savings" istället för "1"
        // Mycket mer läsbart och förståeligt
        public string AccountType { get; set; } = string.Empty;

        // Om kontot är aktivt – klienten kan visa detta i UI
        public bool IsActive { get; set; }

        // När kontot skapades – användbart för kontoutdrag
        public DateTime CreatedAt { get; set; }
    }
}