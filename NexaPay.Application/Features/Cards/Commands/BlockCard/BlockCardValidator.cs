// ============================================================
// BlockCardValidator.cs
// NexaPay.Application/Features/Cards/Commands/BlockCard
// ============================================================

using FluentValidation;

namespace NexaPay.Application.Features.Cards.Commands.BlockCard
{
    public class BlockCardValidator : AbstractValidator<BlockCardCommand>
    {
        public BlockCardValidator()
        {
            // Kortets ID måste finnas
            RuleFor(x => x.CardId)
                .NotEmpty()
                .WithMessage("Kort-ID är obligatoriskt");

            // Anledning är obligatorisk – viktigt för revisionsspår
            // I en bank måste man alltid kunna förklara varför ett kort blockerats
            RuleFor(x => x.Reason)
                .NotEmpty()
                .WithMessage("Anledning till blockering är obligatorisk")
                .MaximumLength(500)
                .WithMessage("Anledningen får inte vara längre än 500 tecken");

            // AdminId måste finnas – sätts från JWT-token
            RuleFor(x => x.AdminId)
                .NotEmpty()
                .WithMessage("Admin-ID är obligatoriskt");
        }
    }
}