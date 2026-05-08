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
            var account = await _unitOfWork.Accounts
                .GetByIdAsync(request.AccountId, cancellationToken);

            if (account == null)
                return Result<IEnumerable<CardDto>>.NotFound(
                    $"Konto med ID {request.AccountId} hittades inte");

            var isOwner = account.OwnerId == request.UserId;

            if (!request.IsStaff && !isOwner)
                return Result<IEnumerable<CardDto>>.NotFound(
                    $"Konto med ID {request.AccountId} hittades inte");

            var cards = await _unitOfWork.Cards
                .GetCardsByAccountIdAsync(request.AccountId, cancellationToken);

            var cardDtos = _mapper.Map<IEnumerable<CardDto>>(cards);
            return Result<IEnumerable<CardDto>>.Success(cardDtos);
        }
    }
}