// ============================================================
// WithdrawHandlerTests.cs
// NexaPay.Tests/Application/Features/Transactions
// ============================================================
// Testar WithdrawHandler med fokus på overdraft-skyddet.
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Transactions.Commands.Withdraw;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Transactions
{
    [TestFixture]
    [Category("Application")]
    [Category("Transactions")]
    [Category("Withdraw")]
    public class WithdrawHandlerTests : TestBase
    {
        private WithdrawHandler _handler = null!;

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

            _handler = new WithdrawHandler(
                MockUnitOfWork.Object,
                Mapper);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad uttag
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att ett giltigt uttag minskar saldot korrekt. " +
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
        [Category("BusinessRule")]
        [Category("Overdraft")]
        [Description(
            "Verifierar det kritiska overdraft-skyddet – " +
            "man ska INTE kunna ta ut mer pengar än vad som finns. " +
            "SaveChangesAsync ska ALDRIG anropas i detta fall.")]
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
        [Category("BusinessRule")]
        [Category("EdgeCase")]
        [Description(
            "Verifierar gränsfallet där Amount == Balance. " +
            "Man ska kunna ta ut exakt hela saldot " +
            "och saldot ska bli 0 efter uttaget.")]
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
        [Category("Security")]
        [Description(
            "Verifierar att en användare INTE kan ta ut pengar " +
            "från ett konto som tillhör någon annan. " +
            "OwnerId måste matcha inloggad UserId.")]
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
        [Category("BusinessRule")]
        [Description(
            "Verifierar att uttag nekas på ett inaktivt konto. " +
            "Ett stängt konto ska inte kunna användas för uttag.")]
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
        [Category("NotFound")]
        [Description(
            "Verifierar att uttag misslyckas när kontot inte " +
            "existerar i databasen. GetByIdAsync returnerar null " +
            "och handleren ska returnera Result.Failure.")]
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