using FluentValidation;

namespace NexaPay.Application.Features.Cards.Commands.UnblockCard
{
    public class UnblockCardValidator : AbstractValidator<UnblockCardCommand>
    {
        public UnblockCardValidator()
        {
            RuleFor(x => x.CardId)
                .NotEmpty()
                .WithMessage("Kort-ID är obligatoriskt");

            RuleFor(x => x.AdminId)
                .NotEmpty()
                .WithMessage("Admin-ID är obligatoriskt");
        }
    }
}
