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

namespace NexaPay.Application.Features.Cards.Commands.CreateCard
{
    public class CreateCardHandler
        : IRequestHandler<CreateCardCommand, Result<CardDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CreateCardHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<CardDto>> Handle(
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
                    return Result<CardDto>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                // Kontot måste tillhöra den inloggade användaren
                // Vi vill inte att någon skapar kort på andras konton
                if (account.OwnerId != request.UserId)
                    return Result<CardDto>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                // Kontot måste vara aktivt
                // Man kan inte skapa kort på ett stängt konto
                if (!account.IsActive)
                    return Result<CardDto>.Failure(
                        "Kan inte skapa kort på ett inaktivt konto");

                // --------------------------------------------------------
                // Steg 2: Generera kortdata
                // --------------------------------------------------------

                // Generera ett unikt 16-siffrigt kortnummer
                // I verkligheten följer detta Luhn-algoritmen
                var cardNumber = GenerateCardNumber();

                // Generera en 3-siffrig CVV-kod
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
                    CVV = cvv,

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

                // Mappa Card → CardDto
                // Observera: CardDto innehåller MaskedCardNumber
                // CVV skickas ALDRIG tillbaka till klienten
                var cardDto = _mapper.Map<CardDto>(card);
                return Result<CardDto>.Success(cardDto);
            }
            catch (Exception ex)
            {
                return Result<CardDto>.Failure(
                    $"Ett fel uppstod när kortet skulle skapas: {ex.Message}");
            }
        }

        // --------------------------------------------------------
        // Hjälpmetoder för att generera kortdata
        // --------------------------------------------------------

        // Genererar ett 16-siffrigt kortnummer
        // Format: fyra grupper om fyra siffror
        // T.ex. "4532123456789010"
        private static string GenerateCardNumber()
        {
            // Börja med 4 – Visa-kort börjar alltid med 4
            var part1 = $"4{Random.Shared.Next(100, 999)}";
            var part2 = Random.Shared.Next(1000, 9999).ToString();
            var part3 = Random.Shared.Next(1000, 9999).ToString();
            var part4 = Random.Shared.Next(1000, 9999).ToString();
            return $"{part1}{part2}{part3}{part4}";
        }

        // Genererar en 3-siffrig CVV-kod
        // T.ex. "123" eller "456"
        private static string GenerateCVV()
        {
            // Next(100, 999) ger alltid ett 3-siffrigt tal
            return Random.Shared.Next(100, 999).ToString();
        }
    }
}