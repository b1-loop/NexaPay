// ============================================================
// GetAllAccountsValidator.cs
// NexaPay.Application/Features/Accounts/Queries/GetAllAccounts
// ============================================================
// FluentValidation-regler för GetAllAccountsQuery.
// ============================================================

using FluentValidation;

namespace NexaPay.Application.Features.Accounts.Queries.GetAllAccounts
{
    public class GetAllAccountsValidator : AbstractValidator<GetAllAccountsQuery>
    {
        public GetAllAccountsValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("Användar-ID är obligatoriskt");
        }
    }
}
