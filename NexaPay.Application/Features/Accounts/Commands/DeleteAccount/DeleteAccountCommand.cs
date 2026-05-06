// ============================================================
// DeleteAccountCommand.cs
// NexaPay.Application/Features/Accounts/Commands/DeleteAccount
// ============================================================
// Command för att stänga/ta bort ett bankkonto.
// Returnerar Result (utan data) – vi behöver bara veta
// om operationen lyckades eller inte.
//
// Säkerhetsregel: Bara Admin eller kontoägaren
// får stänga ett konto.
// Bankregel: Ett konto med saldo > 0 får inte stängas.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;

namespace NexaPay.Application.Features.Accounts.Commands.DeleteAccount
{
    // Result utan <T> – vi returnerar ingen data, bara success/failure
    public record DeleteAccountCommand : IRequest<Result>
    {
        // ID:t för kontot som ska stängas
        public Guid AccountId { get; init; }

        // Den inloggade användarens ID – för behörighetskontroll
        public string UserId { get; init; } = string.Empty;

        // Om användaren är Admin – kan stänga vilket konto som helst
        public bool IsAdmin { get; init; }
    }
}