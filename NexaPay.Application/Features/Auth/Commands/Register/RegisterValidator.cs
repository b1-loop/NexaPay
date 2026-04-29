// ============================================================
// RegisterValidator.cs
// NexaPay.Application/Features/Auth/Commands/Register
// ============================================================

using FluentValidation;
using NexaPay.Application.Common.Constants;

namespace NexaPay.Application.Features.Auth.Commands.Register
{
    public class RegisterValidator : AbstractValidator<RegisterCommand>
    {
        public RegisterValidator()
        {
            // E-post
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("E-postadress är obligatorisk")
                .EmailAddress()
                .WithMessage("Ogiltig e-postadress")
                .MaximumLength(256)
                .WithMessage("E-postadressen är för lång");

            // Lösenord
            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Lösenord är obligatoriskt")
                .MinimumLength(8)
                .WithMessage("Lösenordet måste vara minst 8 tecken")
                .Matches("[A-Z]")
                .WithMessage("Lösenordet måste innehålla minst en stor bokstav")
                .Matches("[a-z]")
                .WithMessage("Lösenordet måste innehålla minst en liten bokstav")
                .Matches("[0-9]")
                .WithMessage("Lösenordet måste innehålla minst en siffra")
                .Matches("[^a-zA-Z0-9]")
                .WithMessage("Lösenordet måste innehålla minst ett specialtecken");

            // Roll – måste vara en av våra definierade roller
            RuleFor(x => x.Role)
                .NotEmpty()
                .WithMessage("Roll är obligatorisk")
                // Must() låter oss skriva en egen valideringsregel
                .Must(role => role == Roles.Admin ||
                              role == Roles.BankManager ||
                              role == Roles.Teller ||
                              role == Roles.Auditor ||
                              role == Roles.User)
                .WithMessage(
                    $"Ogiltig roll. Giltiga roller är: " +
                    $"{Roles.Admin}, {Roles.BankManager}, " +
                    $"{Roles.Teller}, {Roles.Auditor}, {Roles.User}");
        }
    }
}