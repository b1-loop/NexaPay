// ============================================================
// DepositValidator.cs
// NexaPay.Application/Features/Transactions/Commands/Deposit
// ============================================================

using FluentValidation;
using NexaPay.Domain.Policy;

namespace NexaPay.Application.Features.Transactions.Commands.Deposit
{
    public class DepositValidator : AbstractValidator<DepositCommand>
    {
        public DepositValidator()
        {
            RuleFor(x => x.AccountId)
                .NotEmpty()
                .WithMessage("Konto-ID är obligatoriskt");

            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("Insättningsbeloppet måste vara större än 0")
                .LessThanOrEqualTo(TransactionPolicy.MaxTransactionAmount)
                .WithMessage($"Insättningsbeloppet får inte överstiga {TransactionPolicy.MaxTransactionAmount:N0}");

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