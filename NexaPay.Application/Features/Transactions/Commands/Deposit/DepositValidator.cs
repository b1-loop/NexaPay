// ============================================================
// DepositValidator.cs
// NexaPay.Application/Features/Transactions/Commands/Deposit
// ============================================================

using FluentValidation;

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
                // Beloppet måste vara större än 0
                // Man kan inte sätta in 0 kr eller negativa belopp
                .GreaterThan(0)
                .WithMessage("Insättningsbeloppet måste vara större än 0")

                // Max insättning per transaktion – affärsregel
                // I verkligheten finns AML-regler (Anti Money Laundering)
                // som begränsar stora kontanttransaktioner
                .LessThanOrEqualTo(1000000)
                .WithMessage("Insättningsbeloppet får inte överstiga 1 000 000 kr");

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