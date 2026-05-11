using FluentValidation;

namespace NexaPay.Application.Features.Cards.Queries.GetCardsByAccount
{
    public class GetCardsByAccountValidator : AbstractValidator<GetCardsByAccountQuery>
    {
        public GetCardsByAccountValidator()
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
