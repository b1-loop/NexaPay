// ============================================================
// GetAccountByIdHandler.cs
// NexaPay.Application/Features/Accounts/Queries/GetAccountById
// ============================================================
// Hämtar ett specifikt konto och kontrollerar behörighet.
//
// Säkerhetslogik:
//   1. Hämta kontot från databasen
//   2. Om kontot inte finns → returnera Failure (404)
//   3. Om användaren inte är Admin OCH inte äger kontot → Failure (403)
//   4. Returnera kontot som AccountDto
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Accounts.Queries.GetAccountById
{
    public class GetAccountByIdHandler
        : IRequestHandler<GetAccountByIdQuery, Result<AccountDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetAccountByIdHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<AccountDto>> Handle(
            GetAccountByIdQuery request,
            CancellationToken cancellationToken)
        {
            var account = request.IsStaff
                ? await _unitOfWork.Accounts.GetByIdIncludingClosedAsync(request.AccountId, cancellationToken)
                : await _unitOfWork.Accounts.GetByIdAsync(request.AccountId, cancellationToken);

            if (account == null)
                return Result<AccountDto>.NotFound(
                    $"Konto med ID {request.AccountId} hittades inte");

            if (!request.IsStaff && account.OwnerId != request.UserId)
                return Result<AccountDto>.NotFound(
                    $"Konto med ID {request.AccountId} hittades inte");

            var accountDto = _mapper.Map<AccountDto>(account);
            return Result<AccountDto>.Success(accountDto);
        }
    }
}