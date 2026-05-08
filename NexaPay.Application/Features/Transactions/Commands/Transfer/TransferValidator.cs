// ============================================================
// TransferValidator.cs
// NexaPay.Application/Features/Transactions/Commands/Transfer
// ============================================================

using FluentValidation;
using NexaPay.Domain.Policy;

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
                .NotEqual(x => x.FromAccountId)
                .WithMessage("Kan inte överföra pengar till samma konto");

            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("Överföringsbeloppet måste vara större än 0")
                .LessThanOrEqualTo(TransactionPolicy.MaxTransactionAmount)
                .WithMessage($"Överföringsbeloppet får inte överstiga {TransactionPolicy.MaxTransactionAmount:N0}");

            RuleFor(x => x.Description)
                .NotEmpty()
                .WithMessage("Beskrivning är obligatorisk")
                .MaximumLength(TransactionPolicy.MaxDescriptionLength)
                .WithMessage($"Beskrivningen får inte vara längre än {TransactionPolicy.MaxDescriptionLength} tecken");

            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("Användar-ID är obligatoriskt");
        }
    }
}