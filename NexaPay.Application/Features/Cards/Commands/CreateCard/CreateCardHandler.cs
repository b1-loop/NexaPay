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
                // Explicit RNG bytes (128-bit entropy) rather than Guid.NewGuid().
                CardToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
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
            // 15 slumpmässiga siffror (BIN-prefix 4xxx för Visa-format).
            var part1 = $"4{RandomNumberGenerator.GetInt32(100, 1000)}"; // 4 siffror
            var part2 = RandomNumberGenerator.GetInt32(1000, 10000).ToString(); // 4 siffror
            var part3 = RandomNumberGenerator.GetInt32(1000, 10000).ToString(); // 4 siffror
            var part4 = RandomNumberGenerator.GetInt32(100, 1000).ToString();   // 3 siffror
            var partial = $"{part1}{part2}{part3}{part4}"; // 15 siffror
            return $"{partial}{ComputeLuhnCheckDigit(partial)}"; // 16 siffror, Luhn-giltigt
        }

        // Beräknar Luhn-kontrollsiffran för ett partiellt kortnummer (utan check digit).
        // Algoritm: från höger, dubblera varannan siffra (position 1, 3, 5, ... från höger av partial
        // = position 2, 4, 6, ... i det fullständiga kortnumret). Om dubblering ger > 9, subtrahera 9.
        private static int ComputeLuhnCheckDigit(string partialPan)
        {
            var sum = 0;
            for (var i = 0; i < partialPan.Length; i++)
            {
                var digit = partialPan[partialPan.Length - 1 - i] - '0';
                if (i % 2 == 0) // i=0 är rightmost av partial = position 2 i final → dubbleras
                {
                    digit *= 2;
                    if (digit > 9) digit -= 9;
                }
                sum += digit;
            }
            return (10 - (sum % 10)) % 10;
        }

        private static string GenerateCvv()
            => RandomNumberGenerator.GetInt32(100, 1000).ToString();
    }
}