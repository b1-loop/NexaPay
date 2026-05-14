// ============================================================
// PayInvoiceValidator.cs
// NexaPay.Application/Features/Transactions/Commands/PayInvoice
// ============================================================

using FluentValidation;
using NexaPay.Domain.Policy;

namespace NexaPay.Application.Features.Transactions.Commands.PayInvoice
{
    public class PayInvoiceValidator : AbstractValidator<PayInvoiceCommand>
    {
        public PayInvoiceValidator()
        {
            RuleFor(x => x.AccountId)
                .NotEmpty()
                .WithMessage("Konto-ID är obligatoriskt");

            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("Beloppet måste vara större än 0")
                .LessThanOrEqualTo(TransactionPolicy.MaxTransactionAmount)
                .WithMessage($"Beloppet får inte överstiga {TransactionPolicy.MaxTransactionAmount:N0}");

            RuleFor(x => x.Bankgiro)
                .NotEmpty()
                .WithMessage("Bankgiro/plusgiro är obligatoriskt")
                .Must(bg => bg is not null && bg.All(char.IsDigit) && bg.Length is >= 7 and <= 8)
                .WithMessage("Bankgiro/plusgiro måste vara 7–8 siffror");

            RuleFor(x => x.Ocr)
                .NotEmpty()
                .WithMessage("OCR-nummer är obligatoriskt")
                .Must(OcrPolicy.IsValid)
                .WithMessage("OCR-numret är ogiltigt (fel kontrollsiffra eller format)");

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
