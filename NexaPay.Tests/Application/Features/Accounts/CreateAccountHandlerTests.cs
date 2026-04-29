// ============================================================
// CreateAccountHandlerTests.cs
// NexaPay.Tests/Application/Features/Accounts
// ============================================================
// Testar CreateAccountHandler med olika scenarier.
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Accounts.Commands.CreateAccount;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Accounts
{
    [TestFixture]
    [Category("Application")]
    [Category("Accounts")]
    public class CreateAccountHandlerTests : TestBase
    {
        private CreateAccountHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            MockUnitOfWork.Reset();
            MockAccountRepository.Reset();
            MockCardRepository.Reset();
            MockTransactionRepository.Reset();

            MockUnitOfWork
                .Setup(u => u.Accounts)
                .Returns(MockAccountRepository.Object);

            MockUnitOfWork
                .Setup(u => u.Cards)
                .Returns(MockCardRepository.Object);

            MockUnitOfWork
                .Setup(u => u.Transactions)
                .Returns(MockTransactionRepository.Object);

            MockUnitOfWork
                .Setup(u => u.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _handler = new CreateAccountHandler(
                MockUnitOfWork.Object,
                Mapper);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad kontoskapelse
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att ett giltigt CreateAccountCommand " +
            "skapar ett konto med rätt namn, kontotyp och " +
            "att kontot är aktivt från start.")]
        public async Task Handle_WhenValidCommand_ShouldCreateAccount()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = "Mitt sparkonto",
                AccountType = AccountType.Savings,
                OwnerId = "user-123"
            };

            MockAccountRepository
                .Setup(r => r.AccountNumberExistsAsync(
                    It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "ett giltigt command ska skapa ett konto framgångsrikt");

            result.Value!.AccountName.Should().Be("Mitt sparkonto",
                "kontonamnet ska matcha det vi angav i command");

            result.Value.AccountType.Should().Be("Savings",
                "kontotypen ska mappas från enum till sträng i DTO");

            result.Value.IsActive.Should().BeTrue(
                "ett nytt konto ska alltid vara aktivt");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.AtLeastOnce,
                "SaveChangesAsync ska anropas minst en gång");
        }

        // --------------------------------------------------------
        // Test 2: Nytt konto börjar alltid med 0 i saldo
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar den kritiska affärsregeln att ett nytt " +
            "bankkonto ALLTID skapas med 0 kr i saldo. " +
            "Kunden måste göra en insättning separat.")]
        public async Task Handle_WhenAccountCreated_BalanceShouldBeZero()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = "Lönekonto",
                AccountType = AccountType.Checking,
                OwnerId = "user-456"
            };

            MockAccountRepository
                .Setup(r => r.AccountNumberExistsAsync(
                    It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();

            result.Value!.Balance.Should().Be(0,
                "ett nytt konto ska ALLTID börja med 0 kr i saldo");
        }

        // --------------------------------------------------------
        // Test 3: SaveChangesAsync och AddAsync anropas
        // --------------------------------------------------------
        [Test]
        [Category("Database")]
        [Description(
            "Verifierar att handleren faktiskt sparar kontot " +
            "till databasen via UnitOfWork.SaveChangesAsync " +
            "och att AddAsync anropas för att lägga till kontot.")]
        public async Task Handle_WhenAccountCreated_ShouldSaveChanges()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = "Testkonto",
                AccountType = AccountType.ISK,
                OwnerId = "user-789"
            };

            MockAccountRepository
                .Setup(r => r.AccountNumberExistsAsync(
                    It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.AtLeastOnce,
                "SaveChangesAsync ska anropas minst en gång");

            MockAccountRepository.Verify(
                r => r.AddAsync(
                    It.IsAny<NexaPay.Domain.Entities.Account>()),
                Times.AtLeastOnce,
                "AddAsync ska anropas för att lägga till kontot");
        }

        // --------------------------------------------------------
        // Test 4: Olika kontotyper mappas korrekt
        // --------------------------------------------------------
        [TestCase(AccountType.Checking, "Checking")]
        [TestCase(AccountType.Savings, "Savings")]
        [TestCase(AccountType.ISK, "ISK")]
        [Category("Mapping")]
        [Description(
            "Verifierar att alla kontotyper mappas korrekt " +
            "från enum till sträng i AccountDto. " +
            "Klienten ska se 'Savings' inte '1' i API-svaret.")]
        public async Task Handle_WithDifferentAccountTypes_ShouldMapCorrectly(
            AccountType accountType,
            string expectedTypeString)
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = "Testkonto",
                AccountType = accountType,
                OwnerId = "user-123"
            };

            MockAccountRepository
                .Setup(r => r.AccountNumberExistsAsync(
                    It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();

            result.Value!.AccountType.Should().Be(
                expectedTypeString,
                $"kontotyp {accountType} ska mappas till " +
                $"strängen '{expectedTypeString}' i DTO");
        }
    }
}