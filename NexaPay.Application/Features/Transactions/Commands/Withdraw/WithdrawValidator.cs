// ============================================================
// WithdrawValidator.cs
// NexaPay.Application/Features/Transactions/Commands/Withdraw
// ============================================================

using FluentValidation;
using NexaPay.Domain.Policy;

namespace NexaPay.Application.Features.Transactions.Commands.Withdraw
{
    public class WithdrawValidator : AbstractValidator<WithdrawCommand>
    {
        public WithdrawValidator()
        {
            RuleFor(x => x.AccountId)
                .NotEmpty()
                .WithMessage("Konto-ID är obligatoriskt");

            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("Uttagsbeloppet måste vara större än 0")
                .LessThanOrEqualTo(TransactionPolicy.MaxTransactionAmount)
                .WithMessage($"Uttagsbeloppet får inte överstiga {TransactionPolicy.MaxTransactionAmount:N0}");

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