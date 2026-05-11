using FluentValidation;

namespace NexaPay.Application.Features.Accounts.Queries.GetAccountById
{
    public class GetAccountByIdValidator : AbstractValidator<GetAccountByIdQuery>
    {
        public GetAccountByIdValidator()
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
