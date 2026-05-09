using FluentValidation;

namespace NexaPay.Application.Features.Accounts.Commands.UnfreezeAccount
{
    public class UnfreezeAccountValidator : AbstractValidator<UnfreezeAccountCommand>
    {
        public UnfreezeAccountValidator()
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
