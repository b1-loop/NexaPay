// ============================================================
// FreezeAccountValidator.cs
// NexaPay.Application/Features/Accounts/Commands/FreezeAccount
// ============================================================
// FluentValidation-regler för FreezeAccountCommand.
// Kontrollerar att obligatoriska id:n är satta innan
// handlern körs (skyddar mot tomma/Guid.Empty-värden).
// ============================================================

using FluentValidation;

namespace NexaPay.Application.Features.Accounts.Commands.FreezeAccount
{
    public class FreezeAccountValidator : AbstractValidator<FreezeAccountCommand>
    {
        public FreezeAccountValidator()
        {
            RuleFor(x => x.AccountId)
                .NotEmpty()
                .WithMessage("Konto-ID är obligatoriskt");

            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("Användar-ID är obligatoriskt");
        }
    }
}
