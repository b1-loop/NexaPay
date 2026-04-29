// ============================================================
// DepositHandlerTests.cs
// NexaPay.Tests/Application/Features/Transactions
// ============================================================
// Testar DepositHandler med olika scenarier.
//
// Vi testar:
//   1. Lyckad insättning – saldot ökar korrekt
//   2. Konto finns inte – returnerar Failure
//   3. Fel ägare – returnerar Failure
//   4. Inaktivt konto – returnerar Failure
//   5. Transaktionspost skapas med rätt typ
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Transactions.Commands.Deposit;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Transactions
{
    [TestFixture]
    public class DepositHandlerTests : TestBase
    {
        private DepositHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            // Återställ alla mocks innan varje test
            MockUnitOfWork.Reset();
            MockAccountRepository.Reset();
            MockCardRepository.Reset();
            MockTransactionRepository.Reset();

            // Sätt upp mocks på nytt efter reset
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

            _handler = new DepositHandler(
                MockUnitOfWork.Object,
                Mapper);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad insättning
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att en giltig insättning ökar kontosaldot " +
            "med rätt belopp och att en TransactionDto returneras " +
            "med korrekt BalanceAfterTransaction. " +
            "T.ex. saldo 1000 + insättning 500 = 1500.")]
        public async Task Handle_WhenValidDeposit_ShouldIncreaseBalance()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                balance: 1000);

            var command = new DepositCommand
            {
                AccountId = account.Id,
                Amount = 500,
                Description = "Testinsättning",
                UserId = userId
            };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id))
                .ReturnsAsync(account);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "en giltig insättning ska lyckas");

            result.Value!.Amount.Should().Be(500,
                "transaktionsbeloppet ska matcha insättningen");

            result.Value.BalanceAfterTransaction.Should().Be(1500,
                "saldot ska vara 1000 + 500 = 1500 efter insättningen");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once,
                "SaveChangesAsync ska anropas exakt en gång");
        }

        // --------------------------------------------------------
        // Test 2: Konto finns inte
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att en insättning misslyckas med ett " +
            "tydligt felmeddelande när kontot inte existerar. " +
            "SaveChangesAsync ska INTE anropas i detta fall " +
            "– inget ska sparas om kontot inte hittades.")]
        public async Task Handle_WhenAccountNotFound_ShouldReturnFailure()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 500,
                Description = "Testinsättning",
                UserId = "user-123"
            };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(
                    (NexaPay.Domain.Entities.Account?)null);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "om kontot inte finns ska operationen misslyckas");

            result.Error.Should().NotBeEmpty(
                "ett felmeddelande ska finnas");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas om kontot inte hittades");
        }

        // --------------------------------------------------------
        // Test 3: Fel ägare
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att en användare INTE kan sätta in pengar " +
            "på ett konto som tillhör en annan användare. " +
            "Detta är ett kritiskt säkerhetskrav – " +
            "kontots OwnerId måste matcha inloggad UserId.")]
        public async Task Handle_WhenWrongOwner_ShouldReturnFailure()
        {
            // Arrange
            var account = CreateTestAccount(ownerId: "user-123");

            var command = new DepositCommand
            {
                AccountId = account.Id,
                Amount = 500,
                Description = "Testinsättning",
                UserId = "hacker-456"
            };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id))
                .ReturnsAsync(account);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "en användare ska inte kunna sätta in pengar " +
                "på någon annans konto");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas vid fel ägare");
        }

        // --------------------------------------------------------
        // Test 4: Inaktivt konto
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att insättning nekas på ett inaktivt konto. " +
            "Ett stängt konto (IsActive = false) ska inte " +
            "kunna ta emot pengar – detta förhindrar " +
            "transaktioner på avslutade konton.")]
        public async Task Handle_WhenAccountInactive_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                isActive: false);

            var command = new DepositCommand
            {
                AccountId = account.Id,
                Amount = 500,
                Description = "Testinsättning",
                UserId = userId
            };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id))
                .ReturnsAsync(account);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "man ska inte kunna sätta in pengar på ett stängt konto");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas för ett inaktivt konto");
        }

        // --------------------------------------------------------
        // Test 5: Transaktionspost skapas med rätt typ
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att transaktionsposten som skapas vid " +
            "insättning har rätt TransactionType (Deposit) och " +
            "att beskrivningen mappas korrekt till TransactionDto. " +
            "Transaktioner är oföränderliga revisionsposter.")]
        public async Task Handle_WhenValidDeposit_TransactionTypeShouldBeDeposit()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(ownerId: userId, balance: 0);

            var command = new DepositCommand
            {
                AccountId = account.Id,
                Amount = 100,
                Description = "Löneinsättning",
                UserId = userId
            };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id))
                .ReturnsAsync(account);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();

            result.Value!.Type.Should().Be("Deposit",
                "transaktionstypen ska vara Deposit för insättningar");

            result.Value.Description.Should().Be("Löneinsättning",
                "beskrivningen ska matcha det vi angav");
        }
    }
}