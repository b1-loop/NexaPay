// ============================================================
// TransferValidator.cs
// NexaPay.Application/Features/Transactions/Commands/Transfer
// ============================================================

using FluentValidation;

namespace NexaPay.Application.Features.Transactions.Commands.Transfer
{
    public class TransferValidator : AbstractValidator<TransferCommand>
    {
        public TransferValidator()
        {
            RuleFor(x => x.FromAccountId)
                .NotEmpty()
                .WithMessage("Avsändarkonto-ID är obligatoriskt");

            RuleFor(x => x.ToAccountId)
                .NotEmpty()
                .WithMessage("Mottagarkonto-ID är obligatoriskt")
                // Man kan inte överföra pengar till sig själv
                // NotEqual kontrollerar att de två Guid-värdena är olika
                .NotEqual(x => x.FromAccountId)
                .WithMessage("Kan inte överföra pengar till samma konto");

            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("Överföringsbeloppet måste vara större än 0")
                .LessThanOrEqualTo(1000000)
                .WithMessage("Överföringsbeloppet får inte överstiga 1 000 000 kr");

            RuleFor(x => x.Description)
                .NotEmpty()
                .WithMessage("Beskrivning är obligatorisk")
                .MaximumLength(500)
                .WithMessage("Beskrivningen får inte vara längre än 500 tecken");

            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("Användar-ID är obligatoriskt");
        }
    }
}