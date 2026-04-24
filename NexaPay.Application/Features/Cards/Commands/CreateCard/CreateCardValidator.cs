// ============================================================
// CreateCardValidator.cs
// NexaPay.Application/Features/Cards/Commands/CreateCard
// ============================================================

using FluentValidation;

namespace NexaPay.Application.Features.Cards.Commands.CreateCard
{
    public class CreateCardValidator : AbstractValidator<CreateCardCommand>
    {
        public CreateCardValidator()
        {
            // AccountId måste vara ett giltigt Guid
            // Inte Guid.Empty som är "00000000-0000-0000-0000-000000000000"
            RuleFor(x => x.AccountId)
                .NotEmpty()
                .WithMessage("Konto-ID är obligatoriskt");

            // Kortinnehavarens namn måste finnas
            RuleFor(x => x.CardHolderName)
                .NotEmpty()
                .WithMessage("Kortinnehavarens namn är obligatoriskt")

                // Minst 2 tecken – ett enda tecken är inte ett namn
                .MinimumLength(2)
                .WithMessage("Namnet måste vara minst 2 tecken")

                // Max 26 tecken – standarden för namn på bankkort
                // Fysiska kort har begränsat utrymme för text
                .MaximumLength(26)
                .WithMessage("Namnet får inte vara längre än 26 tecken");

            // UserId måste finnas – sätts från JWT-token i controllern
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("Användar-ID är obligatoriskt");
        }
    }
}