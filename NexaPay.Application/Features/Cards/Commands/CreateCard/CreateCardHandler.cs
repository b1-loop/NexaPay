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
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<CreateCardHandler> _logger;

        public CreateCardHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<CreateCardHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<Result<CreateCardResponse>> Handle(
            CreateCardCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                // --------------------------------------------------------
                // Steg 1: Hämta och validera kontot
                // --------------------------------------------------------
                var account = await _unitOfWork.Accounts
                    .GetByIdAsync(request.AccountId);

                // Kontot måste finnas
                if (account == null)
                    return Result<CreateCardResponse>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                // Personal kan skapa kort åt alla kunder
                // Vanliga användare kan bara skapa kort på sina egna konton
                if (!request.IsStaff && account.OwnerId != request.UserId)
                    return Result<CreateCardResponse>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                // Kontot måste vara aktivt
                // Man kan inte skapa kort på ett stängt konto
                if (!account.IsActive)
                    return Result<CreateCardResponse>.Failure(
                        "Kan inte skapa kort på ett inaktivt konto");

                // --------------------------------------------------------
                // Steg 2: Generera kortdata
                // --------------------------------------------------------

                // Generera ett 16-siffrigt kortnummer med CSPRNG.
                // Databasen har UNIQUE-constraint på CardNumber (CardConfiguration)
                // – en eventuell kollision fångas av catch-blocket.
                var cardNumber = GenerateCardNumber();

                // Generera en 3-siffrig CVV-kod – sparas INTE i databasen
                // Returneras en enda gång i svaret (PCI-DSS krav)
                var cvv = GenerateCVV();

                // Utgångsdatum = 3 år från idag
                // Standard för de flesta bankkort
                var expiryDate = DateOnly.FromDateTime(
                    DateTime.UtcNow.AddYears(3));

                // --------------------------------------------------------
                // Steg 3: Skapa Card-objektet
                // --------------------------------------------------------
                var card = new Card
                {
                    Id = Guid.NewGuid(),
                    CardNumber = cardNumber,

                    // Konvertera till versaler – standard för bankkort
                    CardHolderName = request.CardHolderName.ToUpper(),
                    ExpiryDate = expiryDate,

                    // Nytt kort är alltid Inactive från start
                    // Användaren måste aktivera det separat
                    Status = CardStatus.Inactive,

                    // Koppla kortet till rätt konto
                    AccountId = request.AccountId,
                    CreatedAt = DateTime.UtcNow
                };

                // --------------------------------------------------------
                // Steg 4: Spara och returnera
                // --------------------------------------------------------
                await _unitOfWork.Cards.AddAsync(card);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var cardDto = _mapper.Map<CardDto>(card);
                return Result<CreateCardResponse>.Success(new CreateCardResponse
                {
                    Card = cardDto,
                    Cvv = cvv
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oväntat fel vid kortskapning för konto {AccountId}", request.AccountId);
                return Result<CreateCardResponse>.Failure(
                    "Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        // --------------------------------------------------------
        // Hjälpmetoder för att generera kortdata
        // --------------------------------------------------------

        // Genererar ett 16-siffrigt kortnummer med kryptografisk slump
        // Format: 4 + 3 siffror + tre grupper om 4 siffror = 16 siffror totalt
        private static string GenerateCardNumber()
        {
            var part1 = $"4{RandomNumberGenerator.GetInt32(100, 1000)}";
            var part2 = RandomNumberGenerator.GetInt32(1000, 10000).ToString();
            var part3 = RandomNumberGenerator.GetInt32(1000, 10000).ToString();
            var part4 = RandomNumberGenerator.GetInt32(1000, 10000).ToString();
            return $"{part1}{part2}{part3}{part4}";
        }

        // Genererar en 3-siffrig CVV-kod med kryptografisk slump
        private static string GenerateCVV()
        {
            return RandomNumberGenerator.GetInt32(100, 1000).ToString();
        }
    }
}