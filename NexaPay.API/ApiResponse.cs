// ============================================================
// ApiResponse.cs – NexaPay.API
// ============================================================
// En standardiserad svarsklass för alla API-svar.
// Ger konsekvent struktur på alla HTTP-responses vilket
// gör det lättare för klienter att hantera svaren.
//
// Utan denna klass returnerar vi anonyma objekt:
//   return BadRequest(new { message = "fel" });
//
// Med denna klass returnerar vi alltid samma struktur:
//   return BadRequest(ApiResponse.Fail("fel"));
//
// Klienten kan alltid förvänta sig:
//   {
//     "success": true/false,
//     "message": "...",
//     "data": { ... } eller null,
//     "timestamp": "2024-01-01T00:00:00Z"
//   }
// ============================================================

namespace NexaPay.API
{
    public class ApiResponse
    {
        // Om anropet lyckades eller inte
        // true = allt gick bra
        // false = något gick fel
        public bool Success { get; set; }

        // Meddelande till klienten
        // T.ex. "Konto skapades framgångsrikt" eller "Kontot hittades inte"
        public string Message { get; set; } = string.Empty;

        // Valfri data som returneras vid lyckat anrop
        // Null om inget data returneras (t.ex. vid DELETE)
        // Kan vara ett objekt, en lista eller ett enskilt värde
        public object? Data { get; set; }

        // Tidsstämpel för när svaret genererades
        // Användbart för debugging och loggning
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // --------------------------------------------------------
        // Factory Methods – används för att skapa ApiResponse-objekt
        // --------------------------------------------------------
        // Vi använder Factory Methods istället för konstruktorer
        // för att göra koden mer läsbar och uttrycksfull

        // Skapa ett lyckat svar med valfri data och meddelande
        // Exempel: return Ok(ApiResponse.Ok(accountDto, "Konto hämtades"))
        public static ApiResponse Ok(
            object? data = null,
            string message = "Operationen lyckades")
            => new()
            {
                Success = true,
                Message = message,
                Data = data
            };

        // Skapa ett misslyckat svar med felmeddelande
        // Exempel: return BadRequest(ApiResponse.Fail("Kontot hittades inte"))
        public static ApiResponse Fail(string message)
            => new()
            {
                Success = false,
                Message = message,
                Data = null
            };
    }
}