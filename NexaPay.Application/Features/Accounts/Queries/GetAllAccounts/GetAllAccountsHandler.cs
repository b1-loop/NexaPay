// ============================================================
// GetAllAccountsHandler.cs
// NexaPay.Application/Features/Accounts/Queries/GetAllAccounts
// ============================================================
// Hanterar GetAllAccountsQuery och returnerar konton
// baserat på användarens roll (RBAC).
//
// RBAC = Role-Based Access Control:
//   Admin → ser alla konton i systemet
//   User  → ser bara sina egna konton
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Accounts.Queries.GetAllAccounts
{
    public class GetAllAccountsHandler
        : IRequestHandler<GetAllAccountsQuery, Result<IEnumerable<AccountDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetAllAccountsHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<IEnumerable<AccountDto>>> Handle(
            GetAllAccountsQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                // --------------------------------------------------------
                // RBAC – Role-Based Access Control
                // --------------------------------------------------------
                // Beroende på om användaren är Admin eller inte
                // hämtar vi olika data från databasen

                IEnumerable<Domain.Entities.Account> accounts;

                if (request.IsAdmin)
                {
                    // Admin ser ALLA konton i systemet
                    // T.ex. för att övervaka eller hjälpa kunder
                    accounts = await _unitOfWork.Accounts.GetAllAsync();
                }
                else
                {
                    // Vanlig användare ser bara SINA EGNA konton
                    // Vi filtrerar på OwnerId = inloggad användares ID
                    // Detta är ett kritiskt säkerhetskrav
                    accounts = await _unitOfWork.Accounts
                        .GetAccountsByOwnerIdAsync(request.UserId);
                }

                // Mappa listan av Account → listan av AccountDto
                // AutoMapper hanterar hela listan automatiskt
                var accountDtos = _mapper.Map<IEnumerable<AccountDto>>(accounts);

                return Result<IEnumerable<AccountDto>>.Success(accountDtos);
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<AccountDto>>.Failure(
                    $"Ett fel uppstod när konton skulle hämtas: {ex.Message}");
            }
        }
    }
}