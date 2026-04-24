// ============================================================
// GetCardsByAccountHandler.cs
// NexaPay.Application/Features/Cards/Queries/GetCardsByAccount
// ============================================================
// Hämtar alla kort för ett specifikt konto.
//
// Säkerhetslogik:
//   1. Kontot måste finnas
//   2. Användaren måste äga kontot eller vara Admin
//   3. Returnera kortlistan som CardDto
//      (kortnummer är maskerade via AutoMapper)
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Cards.Queries.GetCardsByAccount
{
    public class GetCardsByAccountHandler
        : IRequestHandler<GetCardsByAccountQuery, Result<IEnumerable<CardDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetCardsByAccountHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<IEnumerable<CardDto>>> Handle(
            GetCardsByAccountQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Steg 1: Kontrollera att kontot finns och tillhör användaren
                var account = await _unitOfWork.Accounts
                    .GetByIdAsync(request.AccountId);

                if (account == null)
                    return Result<IEnumerable<CardDto>>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                // Behörighetskontroll – samma mönster som tidigare
                var isOwner = account.OwnerId == request.UserId;

                if (!request.IsAdmin && !isOwner)
                    return Result<IEnumerable<CardDto>>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                // Steg 2: Hämta alla kort för kontot
                var cards = await _unitOfWork.Cards
                    .GetCardsByAccountIdAsync(request.AccountId);

                // Steg 3: Mappa och returnera
                // AutoMapper maskerar kortnumren automatiskt via MappingProfile
                var cardDtos = _mapper.Map<IEnumerable<CardDto>>(cards);
                return Result<IEnumerable<CardDto>>.Success(cardDtos);
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<CardDto>>.Failure(
                    $"Ett fel uppstod: {ex.Message}");
            }
        }
    }
}