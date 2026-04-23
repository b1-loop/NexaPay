// ============================================================
// Result.cs – NexaPay.Application/Common/Models
// ============================================================
// Operation Result-mönstret.
// Istället för att kasta exceptions för förväntade fel
// returnerar vi ett Result-objekt som berättar om
// operationen lyckades eller misslyckades.
//
// Används i ALLA handlers i Application-lagret.
//
// Exempel på användning:
//   return Result<AccountDto>.Success(dto);
//   return Result<AccountDto>.Failure("Kontot finns inte");
// ============================================================

namespace NexaPay.Application.Common.Models
{
    // ============================================================
    // Result utan data – används när vi inte returnerar något värde
    // T.ex. vid Delete-operationer där vi bara vill veta om det gick bra
    // ============================================================
    public class Result
    {
        // Skyddad konstruktor – man skapar inte Result direkt med "new Result()"
        // Man använder istället de statiska metoderna Success() och Failure()
        // Det är ett "Factory Method"-mönster som gör koden mer läsbar
        protected Result(bool isSuccess, string error)
        {
            // Validera att vi inte skickar in ett inkonsekvent tillstånd
            // T.ex. IsSuccess = true MEN med ett felmeddelande är konstigt
            if (isSuccess && error != string.Empty)
                throw new InvalidOperationException("Ett lyckat resultat kan inte ha ett felmeddelande");

            if (!isSuccess && error == string.Empty)
                throw new InvalidOperationException("Ett misslyckat resultat måste ha ett felmeddelande");

            IsSuccess = isSuccess;
            Error = error;
        }

        // Om operationen lyckades – true = lyckades, false = misslyckades
        public bool IsSuccess { get; }

        // Motsatsen till IsSuccess – praktisk för if-satser
        // T.ex. if (result.IsFailure) return BadRequest(result.Error);
        public bool IsFailure => !IsSuccess;

        // Felmeddelandet om operationen misslyckades
        // Tom sträng om operationen lyckades
        public string Error { get; }

        // --------------------------------------------------------
        // Factory Methods – dessa används för att skapa Result-objekt
        // --------------------------------------------------------

        // Skapa ett lyckat resultat utan data
        // T.ex. vid borttagning: Result.Success()
        public static Result Success() => new(true, string.Empty);

        // Skapa ett misslyckat resultat med ett felmeddelande
        // T.ex. Result.Failure("Kontot hittades inte")
        public static Result Failure(string error) => new(false, error);
    }

    // ============================================================
    // Result<T> med data – används när vi returnerar ett värde
    // T.ex. Result<AccountDto> när vi skapar eller hämtar ett konto
    // "<T>" är en typparameter – T kan vara vilken klass som helst
    // ============================================================
    public class Result<T> : Result
    {
        // Privat konstruktor – används via Success() och Failure()
        private Result(bool isSuccess, string error, T? value)
            : base(isSuccess, error) // Anropar basklassens konstruktor
        {
            Value = value;
        }

        // Värdet som returneras vid lyckat resultat
        // Null vid misslyckat resultat
        // "?" efter T betyder att värdet kan vara null
        public T? Value { get; }

        // --------------------------------------------------------
        // Factory Methods för Result<T>
        // --------------------------------------------------------

        // Skapa ett lyckat resultat MED data
        // T.ex. Result<AccountDto>.Success(accountDto)
        public static Result<T> Success(T value) => new(true, string.Empty, value);

        // Skapa ett misslyckat resultat utan data
        // T.ex. Result<AccountDto>.Failure("Kontot hittades inte")
        public static new Result<T> Failure(string error) => new(false, error, default);
    }
}