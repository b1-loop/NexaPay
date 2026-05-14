using NexaPay.Domain.Enums;

namespace NexaPay.API.Contracts
{
    public record CreateAccountRequest
    {
        public string AccountName { get; init; } = string.Empty;
        public AccountType AccountType { get; init; }

        // Valfritt – personal kan ange en kunds e-post för att skapa kontot
        // åt den användaren. Lämnas tomt skapas kontot åt den inloggade.
        public string? OwnerEmail { get; init; }
    }
}
