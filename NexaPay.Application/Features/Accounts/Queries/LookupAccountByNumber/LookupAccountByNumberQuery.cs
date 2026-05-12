using MediatR;
using NexaPay.Application.Common.Models;

namespace NexaPay.Application.Features.Accounts.Queries.LookupAccountByNumber
{
    public class LookupAccountByNumberQuery : IRequest<Result<AccountLookupDto>>
    {
        public string AccountNumber { get; set; } = string.Empty;
    }

    public class AccountLookupDto
    {
        public Guid Id { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
    }
}
