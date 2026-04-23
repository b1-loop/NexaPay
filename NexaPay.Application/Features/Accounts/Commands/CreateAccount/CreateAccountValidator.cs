// ============================================================
// CreateAccountValidator.cs
// NexaPay.Application/Features/Accounts/Commands/CreateAccount
// ============================================================
// Definierar valideringsreglerna för CreateAccountCommand.
// Körs automatiskt av ValidationBehavior i pipeline
// INNAN handleren körs.
//
// AbstractValidator<T> är FluentValidations basklass.
// Vi definierar regler i konstruktorn med RuleFor().
// ============================================================

using FluentValidation;

namespace NexaPay.Application.Features.Accounts.Commands.CreateAccount
{
    // AbstractValidator<CreateAccountCommand> betyder att vi
    // validerar ett CreateAccountCommand-objekt
    public class CreateAccountValidator
        : AbstractValidator<CreateAccountCommand>
    {
        public CreateAccountValidator()
        {
            // --------------------------------------------------------
            // Regel för AccountName
            // --------------------------------------------------------
            RuleFor(x => x.AccountName)
                // Får inte vara tom eller null
                .NotEmpty()
                .WithMessage("Kontonamn är obligatoriskt")

                // Minst 2 tecken – ett enda tecken är inte ett riktigt namn
                .MinimumLength(2)
                .WithMessage("Kontonamnet måste vara minst 2 tecken")

                // Max 100 tecken – rimlig gräns för ett kontonamn
                .MaximumLength(100)
                .WithMessage("Kontonamnet får inte vara längre än 100 tecken");

            // --------------------------------------------------------
            // Regel för AccountType
            // --------------------------------------------------------
            RuleFor(x => x.AccountType)
                // Kontrollera att värdet är ett giltigt enum-värde
                // IsInEnum() kontrollerar att värdet finns i AccountType-enum
                // Skyddar mot att klienten skickar in ett ogiltigt heltal
                .IsInEnum()
                .WithMessage("Ogiltig kontotyp. Välj Checking, Savings eller ISK");

            // --------------------------------------------------------
            // Regel för OwnerId
            // --------------------------------------------------------
            RuleFor(x => x.OwnerId)
                // OwnerId måste finnas – den sätts från JWT-token
                // Om den är tom har något gått fel med autentiseringen
                .NotEmpty()
                .WithMessage("Ägar-ID är obligatoriskt");
        }
    }
}