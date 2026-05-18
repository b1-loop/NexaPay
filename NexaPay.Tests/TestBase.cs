// ============================================================
// TestBase.cs – NexaPay.Tests
// ============================================================
// En basklass som alla tester kan ärva från.
// Innehåller gemensam setup och hjälpmetoder som
// återanvänds i alla testklasser.
//
// Vad är en mock?
// En mock är ett fejkat objekt som simulerar ett riktigt objekt.
// T.ex. istället för en riktig databas använder vi en mock
// som låtsas vara en databas – snabbt och utan sidoeffekter.
//
// Varför TestBase?
// Istället för att skriva samma setup-kod i varje testklass
// samlar vi det här och låter testklasserna ärva det.
// ============================================================

using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NexaPay.Application.Mappings;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NexaPay.Domain.Interfaces;
using NexaPay.Domain.ValueObjects;

namespace NexaPay.Tests
{
    // abstract = kan inte instansieras direkt
    // Används bara som basklass för specifika testklasser
    public abstract class TestBase
    {
        // --------------------------------------------------------
        // Mock-objekt – fejkade implementationer av interfaces
        // --------------------------------------------------------

        // Mock av IUnitOfWork – simulerar databastransaktioner
        // Istället för att prata med en riktig databas
        // kontrollerar vi exakt vad som returneras
        protected readonly Mock<IUnitOfWork> MockUnitOfWork;

        // Mock av specifika repositories
        // Vi mockar dessa separat för mer kontroll i testerna
        protected readonly Mock<IAccountRepository> MockAccountRepository;
        protected readonly Mock<ICardRepository> MockCardRepository;
        protected readonly Mock<ITransactionRepository> MockTransactionRepository;

        // IMapper – vi använder den riktiga AutoMapper-instansen
        // med vår MappingProfile för att testa mappningarna också
        protected readonly IMapper Mapper;

        // --------------------------------------------------------
        // Konstruktor – körs innan varje testklass initieras
        // --------------------------------------------------------
        protected TestBase()
        {
            // --------------------------------------------------------
            // Skapa mock-objekt
            // --------------------------------------------------------
            MockAccountRepository = new Mock<IAccountRepository>();
            MockCardRepository = new Mock<ICardRepository>();
            MockTransactionRepository = new Mock<ITransactionRepository>();
            MockUnitOfWork = new Mock<IUnitOfWork>();

            // --------------------------------------------------------
            // Koppla repositories till UnitOfWork-mocken
            // --------------------------------------------------------
            // När koden anropar _unitOfWork.Accounts returneras vår mock
            // Samma för Cards och Transactions
            MockUnitOfWork
                .Setup(u => u.Accounts)
                .Returns(MockAccountRepository.Object);

            MockUnitOfWork
                .Setup(u => u.Cards)
                .Returns(MockCardRepository.Object);

            MockUnitOfWork
                .Setup(u => u.Transactions)
                .Returns(MockTransactionRepository.Object);

            // --------------------------------------------------------
            // Konfigurera SaveChangesAsync
            // --------------------------------------------------------
            // Returnerar 1 = en rad påverkades i databasen (lyckat)
            // It.IsAny<CancellationToken>() = acceptera vilken token som helst
            MockUnitOfWork
                .Setup(u => u.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // --------------------------------------------------------
            // Skapa AutoMapper med NullLoggerFactory
            // --------------------------------------------------------
            // AutoMapper 16+ kräver en ILoggerFactory i konstruktorn
            // Vi använder NullLoggerFactory som inte loggar något
            // Det är perfekt för tester där vi inte vill ha loggning
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                // Lägg till vår MappingProfile med alla CreateMap<>()-regler
                // Vi testar med riktig mappning – inte en mock
                // Det gör att vi även testar att mappningarna är korrekta
                cfg.AddProfile(new MappingProfile());
            }, NullLoggerFactory.Instance);
            // NullLoggerFactory.Instance = en logger som kastar bort allt
            // Används när vi inte vill ha riktig loggning

            // Skapa och spara IMapper-instansen
            Mapper = mapperConfig.CreateMapper();
        }

        // --------------------------------------------------------
        // Hjälpmetoder för att skapa testdata
        // --------------------------------------------------------
        // Dessa metoder skapar färdiga objekt för testning
        // Parametrar har standardvärden så vi kan anpassa vid behov
        // T.ex. CreateTestAccount(balance: 0) för ett tomt konto

        // Skapar ett giltigt konto för testning
        protected static Account CreateTestAccount(
            string ownerId = "test-user-id",
            decimal balance = 1000,
            bool isActive = true)
        {
            var account = Account.Open("SE123456789", "Testkonto", AccountType.Checking, ownerId);

            if (isActive)
            {
                if (balance > 0)
                    account.Deposit(new Money(balance, Currency.SEK), "Test initial deposit");
            }
            else
            {
                // Inactive accounts must have zero balance in the domain.
                // Tests using isActive:false check the IsActive guard, which
                // fires before any balance check, so balance=0 is equivalent.
                account.Close();
            }

            return account;
        }

        // Skapar ett giltigt kort för testning
        protected static Card CreateTestCard(
            Guid accountId = default,
            CardStatus status = CardStatus.Active)
        {
            var card = Card.Issue(
                cardToken: Guid.NewGuid().ToString(),
                last4Digits: "9010",
                cardHolderName: "TEST USER",
                expiryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3)),
                accountId: accountId == default ? Guid.NewGuid() : accountId);

            switch (status)
            {
                case CardStatus.Active:
                    card.Activate();
                    break;
                case CardStatus.Blocked:
                    card.Block();
                    break;
                case CardStatus.Expired:
                    card.MarkAsExpired();
                    break;
                // CardStatus.Inactive is the default — no action needed
            }

            return card;
        }

        // Skapar en giltig transaktion för testning
        protected static Transaction CreateTestTransaction(
            Guid accountId = default,
            decimal amount = 100)
        {
            var money = new Money(amount, Currency.SEK);
            return new Transaction
            {
                Id = Guid.NewGuid(),
                Amount = money,
                Type = TransactionType.Deposit,
                Description = "Testinsättning",
                BalanceAfterTransaction = money,
                AccountId = accountId == default ? Guid.NewGuid() : accountId,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}