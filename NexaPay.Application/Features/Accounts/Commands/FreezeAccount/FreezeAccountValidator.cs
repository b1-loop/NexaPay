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
