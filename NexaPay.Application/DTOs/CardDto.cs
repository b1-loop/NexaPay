// ============================================================
// CardDto.cs – NexaPay.Application/DTOs
// ============================================================
// Data Transfer Object för Card.
//
// Säkerhetsnotering:
// Vi maskerar kortnumret – klienten ser bara de sista 4 siffrorna
// T.ex. "**** **** **** 9010" istället för hela numret
// CVV skickas ALDRIG till klienten – det är ett säkerhetskrav
// ============================================================

namespace NexaPay.Application.DTOs
{
    public class CardDto
    {
        // Kortets unika ID
        public Guid Id { get; set; }

        // Maskerat kortnummer – bara sista 4 siffrorna visas
        // T.ex. "**** **** **** 9010"
        // AutoMapper-profilen hanterar maskeringen
        public string MaskedCardNumber { get; set; } = string.Empty;

        // Kortinnehavarens namn
        public string CardHolderName { get; set; } = string.Empty;

        // Utgångsdatum – klienten behöver se detta
        public DateOnly ExpiryDate { get; set; }

        // Kortets status som text – "Active", "Blocked", "Expired", "Inactive"
        public string Status { get; set; } = string.Empty;

        // Vilket konto kortet tillhör
        public Guid AccountId { get; set; }

        // När kortet skapades
        public DateTime CreatedAt { get; set; }

        // OBS: CVV inkluderas ALDRIG i en DTO – säkerhetskrav
    }
}