// ============================================================
// ApiResponse.cs – NexaPay.API
// ============================================================
// Standardiserat svar-omslag som alla controllers returnerar:
//   { success: bool, message: string, data?: T, timestamp: DateTime }
//
// Konsekvent format gör att frontend kan ha EN felhantering
// (axios response-interceptor) som fungerar för alla anrop.
// Fabriksmetoderna Ok/Fail garanterar att success-flaggan
// alltid är rätt i förhållande till om vi har data eller fel.
// ============================================================

namespace NexaPay.API
{
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public static ApiResponse Ok(string message = "Operationen lyckades")
            => new() { Success = true, Message = message };

        public static ApiResponse<T> Ok<T>(T data, string message = "Operationen lyckades")
            => new() { Success = true, Message = message, Data = data };

        public static ApiResponse Fail(string message)
            => new() { Success = false, Message = message };
    }

    public class ApiResponse<T> : ApiResponse
    {
        public T? Data { get; set; }
    }
}
