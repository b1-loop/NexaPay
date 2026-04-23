// ============================================================
// DeleteAccountValidator.cs
// NexaPay.Application/Features/Accounts/Commands/DeleteAccount
// ============================================================

using FluentValidation;

namespace NexaPay.Application.Features.Accounts.Commands.DeleteAccount
{
    public class DeleteAccountValidator
        : AbstractValidator<DeleteAccountCommand>
    {
        public DeleteAccountValidator()
        {
            // AccountId får inte vara en tom Guid
            // Guid.Empty = "00000000-0000-0000-0000-000000000000"
            // Om det är en tom Guid har något gått fel i controllern
            RuleFor(x => x.AccountId)
                .NotEmpty()
                .WithMessage("Konto-ID är obligatoriskt");

            // UserId måste finnas – sätts från JWT-token
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("Användar-ID är obligatoriskt");
        }
    }
}