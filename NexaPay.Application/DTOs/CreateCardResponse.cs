namespace NexaPay.Application.DTOs
{
    public class CreateCardResponse
    {
        public CardDto Card { get; set; } = null!;
        public string Cvv { get; set; } = string.Empty;
    }
}
