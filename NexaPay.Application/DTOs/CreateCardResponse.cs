namespace NexaPay.Application.DTOs
{
    public class CreateCardResponse
    {
        public CardDto Card { get; set; } = null!;
        // Full PAN returned exactly once at creation — never stored or repeated.
        public string CardNumber { get; set; } = string.Empty;
        public string Cvv { get; set; } = string.Empty;
    }
}
