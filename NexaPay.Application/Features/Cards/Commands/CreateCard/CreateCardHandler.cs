// ============================================================
// CreateCardHandler.cs
// NexaPay.Application/Features/Cards/Commands/CreateCard
// ============================================================
// Skapar ett nytt bankkort kopplat till ett specifikt konto.
//
// Affärsregler:
//   1. Kontot måste finnas och tillhöra användaren
//   2. Kontot måste vara aktivt
//   3. Generera kortnummer, CVV och utgångsdatum
//   4. Kortet skapas alltid med status Inactive
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NexaPay.Domain.Interfaces;
using System.Security.Cryptography;

namespace NexaPay.Application.Features.Cards.Commands.CreateCard
{
    public class CreateCardHandler
        : IRequestHandler<CreateCardCommand, Result<CreateCardResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CreateCardHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<CreateCardResponse>> Handle(
            CreateCardCommand request,
            CancellationToken cancellationToken)
        {
            var account = await _unitOfWork.Accounts
                .GetByIdAsync(request.AccountId, cancellationToken);

            if (account == null)
                return Result<CreateCardResponse>.NotFound(
                    $"Konto med ID {request.AccountId} hittades inte");

            if (!request.IsStaff && account.OwnerId != request.UserId)
                return Result<CreateCardResponse>.NotFound(
                    $"Konto med ID {request.AccountId} hittades inte");

            if (account.Status != AccountStatus.Open)
                return Result<CreateCardResponse>.Failure(
                    $"Kan inte skapa kort på ett {account.Status.ToString().ToLower()} konto");

            var pan = GeneratePan();
            var last4 = pan[^4..];
            var cvv = GenerateCvv();
            var expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3));

            var card = new Card
            {
                Id = Guid.NewGuid(),
                // Full PAN is never persisted — only an opaque token + last 4 digits.
                CardToken = Guid.NewGuid().ToString(),
                Last4Digits = last4,
                CardHolderName = request.CardHolderName.ToUpper(),
                ExpiryDate = expiryDate,
                AccountId = request.AccountId,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Cards.AddAsync(card, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var cardDto = _mapper.Map<CardDto>(card);
            return Result<CreateCardResponse>.Success(new CreateCardResponse
            {
                Card = cardDto,
                CardNumber = pan,
                Cvv = cvv
            });
        }

        // --------------------------------------------------------
        // Hjälpmetoder för att generera kortdata
        // --------------------------------------------------------

        private static string GeneratePan()
        {
            var part1 = $"4{RandomNumberGenerator.GetInt32(100, 1000)}";
            var part2 = RandomNumberGenerator.GetInt32(1000, 10000).ToString();
            var part3 = RandomNumberGenerator.GetInt32(1000, 10000).ToString();
            var part4 = RandomNumberGenerator.GetInt32(1000, 10000).ToString();
            return $"{part1}{part2}{part3}{part4}";
        }

        private static string GenerateCvv()
            => RandomNumberGenerator.GetInt32(100, 1000).ToString();
    }
}