// ============================================================
// WithdrawHandlerTests.cs
// NexaPay.Tests/Application/Features/Transactions
// ============================================================
// Testar WithdrawHandler med fokus på overdraft-skyddet.
//
// Vi testar:
//   1. Lyckad uttag – saldot minskar korrekt
//   2. Overdraft-skydd – saldot räcker inte
//   3. Exakt saldo – ta ut hela beloppet
//   4. Fel ägare – returnerar Failure
//   5. Inaktivt konto – returnerar Failure
//   6. Konto finns inte – returnerar Failure
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Transactions.Commands.Withdraw;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Transactions
{
    [TestFixture]
    public class WithdrawHandlerTests : TestBase
    {
        private WithdrawHandler _handler = null!;

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

            _handler = new WithdrawHandler(
                MockUnitOfWork.Object,
                Mapper);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad uttag
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att ett giltigt uttag minskar saldot korrekt " +
            "och att TransactionDto returneras med rätt värden. " +
            "T.ex. saldo 1000 - uttag 300 = 700 kvar på kontot.")]
        public async Task Handle_WhenValidWithdrawal_ShouldDecreaseBalance()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                balance: 1000);

            var command = new WithdrawCommand
            {
                AccountId = account.Id,
                Amount = 300,
                Description = "Testuttag",
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
                "ett giltigt uttag ska lyckas");

            result.Value!.BalanceAfterTransaction.Should().Be(700,
                "saldot ska vara 1000 - 300 = 700 efter uttaget");

            result.Value.Amount.Should().Be(300,
                "uttagsbeloppet ska vara 300");

            result.Value.Type.Should().Be("Withdrawal",
                "transaktionstypen ska vara Withdrawal för uttag");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once,
                "SaveChangesAsync ska anropas exakt en gång");
        }

        // --------------------------------------------------------
        // Test 2: Overdraft-skydd
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar det kritiska overdraft-skyddet – " +
            "man ska INTE kunna ta ut mer pengar än vad som finns. " +
            "Saldot ska förbli oförändrat och SaveChangesAsync " +
            "ska ALDRIG anropas när saldot inte räcker.")]
        public async Task Handle_WhenInsufficientBalance_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                balance: 100);

            var command = new WithdrawCommand
            {
                AccountId = account.Id,
                Amount = 500,
                Description = "Testuttag",
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
                "man ska inte kunna ta ut mer pengar än vad som finns!");

            result.Error.Should().Contain("saldo",
                "felmeddelandet ska nämna saldot");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas när saldot inte räcker!");
        }

        // --------------------------------------------------------
        // Test 3: Exakt saldo
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att man kan ta ut exakt hela saldot " +
            "och att saldot blir 0 efter uttaget. " +
            "Gränsfallet där Amount == Balance ska tillåtas.")]
        public async Task Handle_WhenWithdrawingExactBalance_ShouldSucceed()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                balance: 500);

            var command = new WithdrawCommand
            {
                AccountId = account.Id,
                Amount = 500,
                Description = "Tömmer kontot",
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
                "man ska kunna ta ut exakt hela saldot");

            result.Value!.BalanceAfterTransaction.Should().Be(0,
                "saldot ska vara 0 efter att hela beloppet tagits ut");
        }

        // --------------------------------------------------------
        // Test 4: Fel ägare
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att en användare INTE kan ta ut pengar " +
            "från ett konto som tillhör någon annan. " +
            "OwnerId måste matcha inloggad UserId – " +
            "annars returneras Failure utan att något sparas.")]
        public async Task Handle_WhenWrongOwner_ShouldReturnFailure()
        {
            // Arrange
            var account = CreateTestAccount(ownerId: "user-123");

            var command = new WithdrawCommand
            {
                AccountId = account.Id,
                Amount = 100,
                Description = "Testuttag",
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
                "en användare ska inte kunna ta ut pengar " +
                "från någon annans konto");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas vid fel ägare");
        }

        // --------------------------------------------------------
        // Test 5: Inaktivt konto
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att uttag nekas på ett inaktivt konto. " +
            "Ett stängt konto (IsActive = false) ska inte " +
            "kunna användas för uttag – detta förhindrar " +
            "transaktioner på avslutade konton.")]
        public async Task Handle_WhenAccountInactive_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                isActive: false);

            var command = new WithdrawCommand
            {
                AccountId = account.Id,
                Amount = 100,
                Description = "Testuttag",
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
                "man ska inte kunna ta ut pengar från ett stängt konto");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas för ett inaktivt konto");
        }

        // --------------------------------------------------------
        // Test 6: Konto finns inte
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att uttag misslyckas med ett tydligt " +
            "felmeddelande när kontot inte existerar i databasen. " +
            "GetByIdAsync returnerar null och handleren " +
            "ska returnera Result.Failure utan att spara något.")]
        public async Task Handle_WhenAccountNotFound_ShouldReturnFailure()
        {
            // Arrange
            var command = new WithdrawCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 100,
                Description = "Testuttag",
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
                "ett uttag ska misslyckas om kontot inte finns");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas om kontot inte hittades");
        }
    }
}