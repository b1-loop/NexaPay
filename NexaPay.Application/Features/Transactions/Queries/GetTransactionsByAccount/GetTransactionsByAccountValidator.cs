// ============================================================
// GetTransactionsByAccountValidator.cs
// NexaPay.Application/Features/Transactions/Queries/GetTransactionsByAccount
// ============================================================
// FluentValidation-regler för transaktionspagineringen. Page
// måste vara minst 1 och PageSize 1..100 – samma gränser som
// handlern klampar till, men här returneras ett tydligt 400-fel.
// ============================================================

using FluentValidation;
using NexaPay.Application.Features.Transactions.Queries.GetTransactionsByAccount;

namespace NexaPay.Application.Features.Transactions.Queries.GetTransactionsByAccount
{
    public class GetTransactionsByAccountValidator : AbstractValidator<GetTransactionsByAccountQuery>
    {
        public GetTransactionsByAccountValidator()
        {
            RuleFor(x => x.AccountId)
                .NotEmpty()
                .WithMessage("Konto-ID är obligatoriskt");

            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("Användar-ID är obligatoriskt");

            RuleFor(x => x.Page)
                .GreaterThanOrEqualTo(1)
                .WithMessage("Sidnummer måste vara minst 1");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100)
                .WithMessage("Sidstorlek måste vara mellan 1 och 100");
        }
    }
}
