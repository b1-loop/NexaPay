// ============================================================
// WithdrawValidator.cs
// NexaPay.Application/Features/Transactions/Commands/Withdraw
// ============================================================

using FluentValidation;

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
                // Beloppet måste vara positivt
                .GreaterThan(0)
                .WithMessage("Uttagsbeloppet måste vara större än 0")

                // Max uttag per transaktion
                .LessThanOrEqualTo(1000000)
                .WithMessage("Uttagsbeloppet får inte överstiga 1 000 000 kr");

            // OBS: Vi kontrollerar INTE om saldot räcker här!
            // Saldokontrollen görs i handleren eftersom vi behöver
            // databasen för att kolla saldot – validators har ingen
            // tillgång till databasen (och ska inte ha det)

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