// ============================================================
// GetAllAccountsQuery.cs
// NexaPay.Application/Features/Accounts/Queries/GetAllAccounts
// ============================================================
// En Query är ett request-objekt för LÄSOPERATIONER.
// Den ändrar ingen data – den hämtar bara.
//
// RBAC-logik:
//   - Om användaren är Admin → returnera ALLA konton
//   - Om användaren är User  → returnera bara EGNA konton
//
// Denna logik hanteras i handleren baserat på
// UserId och IsAdmin som skickas med från controllern.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Features.Accounts.Queries.GetAllAccounts
{
    public record GetAllAccountsQuery : IRequest<Result<IEnumerable<AccountDto>>>
    {
        // ID:t för den inloggade användaren – hämtas från JWT-token
        // Används för att filtrera konton om användaren inte är Admin
        public string UserId { get; init; } = string.Empty;

        // Om användaren har Admin-rollen – hämtas från JWT-token
        // true = returnera alla konton
        // false = returnera bara användarens egna konton
        public bool IsAdmin { get; init; }
    }
}