// ============================================================
// RegisterValidator.cs
// NexaPay.Application/Features/Auth/Commands/Register
// ============================================================
// Validerar RegisterCommand innan handleren körs.
// Körs automatiskt av ValidationBehavior i pipeline.
// ============================================================

using FluentValidation;

namespace NexaPay.Application.Features.Auth.Commands.Register
{
    public class RegisterValidator : AbstractValidator<RegisterCommand>
    {
        public RegisterValidator()
        {
            // ------------------------------------------------
            // E-post
            // ------------------------------------------------
            RuleFor(x => x.Email)
                // Får inte vara tom
                .NotEmpty()
                .WithMessage("E-postadress är obligatorisk")

                // Måste vara en giltig e-postadress
                // EmailAddress() kontrollerar format: xxx@xxx.xxx
                .EmailAddress()
                .WithMessage("Ogiltig e-postadress")

                // Max 256 tecken – standard för e-postadresser
                .MaximumLength(256)
                .WithMessage("E-postadressen är för lång");

            // ------------------------------------------------
            // Lösenord
            // ------------------------------------------------
            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Lösenord är obligatoriskt")

                // Minst 8 tecken – matchar Identity-kravet
                .MinimumLength(8)
                .WithMessage("Lösenordet måste vara minst 8 tecken")

                // Kräv stor bokstav
                .Matches("[A-Z]")
                .WithMessage("Lösenordet måste innehålla minst en stor bokstav")

                // Kräv liten bokstav
                .Matches("[a-z]")
                .WithMessage("Lösenordet måste innehålla minst en liten bokstav")

                // Kräv siffra
                .Matches("[0-9]")
                .WithMessage("Lösenordet måste innehålla minst en siffra")

                // Kräv specialtecken
                .Matches("[^a-zA-Z0-9]")
                .WithMessage("Lösenordet måste innehålla minst ett specialtecken");
        }
    }
}