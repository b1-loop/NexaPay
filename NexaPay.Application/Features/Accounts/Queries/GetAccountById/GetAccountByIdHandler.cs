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
            try
            {
                // --------------------------------------------------------
                // Steg 1: Hämta kontot från databasen
                // --------------------------------------------------------
                var account = await _unitOfWork.Accounts
                    .GetByIdAsync(request.AccountId);

                // Om kontot inte finns – returnera Failure
                // Controllern tolkar detta som 404 Not Found
                if (account == null)
                {
                    return Result<AccountDto>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");
                }

                // --------------------------------------------------------
                // Steg 2: Kontrollera behörighet (RBAC)
                // --------------------------------------------------------
                // En användare får bara se sitt EGET konto
                // Admin får se vilket konto som helst
                var isOwner = account.OwnerId == request.UserId;

                if (!request.IsAdmin && !isOwner)
                {
                    // Användaren försöker komma åt någon annans konto!
                    // Vi returnerar samma fel som "inte hittat" av säkerhetsskäl
                    // Vi vill inte avslöja att kontot finns men ägs av någon annan
                    return Result<AccountDto>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");
                }

                // --------------------------------------------------------
                // Steg 3: Mappa och returnera
                // --------------------------------------------------------
                var accountDto = _mapper.Map<AccountDto>(account);
                return Result<AccountDto>.Success(accountDto);
            }
            catch (Exception ex)
            {
                return Result<AccountDto>.Failure(
                    $"Ett fel uppstod: {ex.Message}");
            }
        }
    }
}