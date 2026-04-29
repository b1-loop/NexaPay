// ============================================================
// LoginValidator.cs
// NexaPay.Application/Features/Auth/Commands/Login
// ============================================================

using FluentValidation;

namespace NexaPay.Application.Features.Auth.Commands.Login
{
    public class LoginValidator : AbstractValidator<LoginCommand>
    {
        public LoginValidator()
        {
            // E-post måste finnas och vara giltig
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("E-postadress är obligatorisk")
                .EmailAddress()
                .WithMessage("Ogiltig e-postadress");

            // Lösenord måste finnas
            // Vi validerar inte format här – det är inloggning,
            // inte registrering. Fel lösenord hanteras av AuthService.
            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Lösenord är obligatoriskt");
        }
    }
}