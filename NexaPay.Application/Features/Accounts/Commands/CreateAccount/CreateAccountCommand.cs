// ============================================================
// CreateAccountCommand.cs
// NexaPay.Application/Features/Accounts/Commands/CreateAccount
// ============================================================
// Ett Command är ett request-objekt som bär med sig all data
// som behövs för att skapa ett nytt bankkonto.
//
// IRequest<Result<AccountDto>> betyder:
//   - Detta är ett MediatR request
//   - När det är klart returneras ett Result<AccountDto>
//   - Result = lyckades eller misslyckades operationen?
//   - AccountDto = datan som returneras vid lyckat resultat
//
// "record" används istället för "class" eftersom:
//   - Commands är oföränderliga (immutable) – vi ändrar dem aldrig
//   - Records ger automatisk equals och toString
//   - Kortare och tydligare syntax
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Enums;

namespace NexaPay.Application.Features.Accounts.Commands.CreateAccount
{
    public record CreateAccountCommand : IRequest<Result<AccountDto>>
    {
        // Namnet som användaren ger sitt konto
        // T.ex. "Mitt sparkonto" eller "Hushållskassan"
        public string AccountName { get; init; } = string.Empty;

        // Typen av konto – Checking, Savings eller ISK
        // "init" = kan bara sättas vid skapandet, inte ändras efteråt
        public AccountType AccountType { get; init; }

        // ID:t för användaren som äger kontot
        // Sätts från JWT-token i controllern – inte från klienten direkt
        // Det är viktigt att vi inte låter klienten bestämma vem som äger kontot
        public string OwnerId { get; init; } = string.Empty;
    }
}