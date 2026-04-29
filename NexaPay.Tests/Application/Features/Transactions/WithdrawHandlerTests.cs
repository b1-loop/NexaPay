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
//
// Overdraft-skyddet är den VIKTIGASTE affärsregeln
// i hela NexaPay – det är kritiskt att det testas noggrant!
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
            _handler = new WithdrawHandler(
                MockUnitOfWork.Object,
                Mapper);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad uttag
        // --------------------------------------------------------
        [Test]
        public async Task Handle_WhenValidWithdrawal_ShouldDecreaseBalance()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                balance: 1000); // 1000 kr på kontot

            var command = new WithdrawCommand
            {
                AccountId = account.Id,
                Amount = 300, // Ta ut 300 kr
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

            // Verifiera att ändringar sparades
            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once,
                "SaveChangesAsync ska anropas exakt en gång");
        }

        // --------------------------------------------------------
        // Test 2: Overdraft-skydd – saldot räcker inte
        // --------------------------------------------------------
        [Test]
        public async Task Handle_WhenInsufficientBalance_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                balance: 100); // Bara 100 kr på kontot

            var command = new WithdrawCommand
            {
                AccountId = account.Id,
                Amount = 500, // Försöker ta ut 500 kr!
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
                "man ska inte kunna ta ut mer pengar än vad som finns " +
                "– overdraft-skyddet är en kritisk affärsregel!");

            result.Error.Should().Contain("saldo",
                "felmeddelandet ska nämna saldot så " +
                "användaren förstår varför det misslyckades");

            // KRITISKT: Verifiera att INGET sparades
            // Saldot ska INTE ha ändrats om uttaget misslyckades
            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas när saldot inte räcker – " +
                "annars kan pengar försvinna!");
        }

        // --------------------------------------------------------
        // Test 3: Exakt saldo – ta ut hela beloppet
        // --------------------------------------------------------
        [Test]
        public async Task Handle_WhenWithdrawingExactBalance_ShouldSucceed()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                balance: 500); // Exakt 500 kr

            var command = new WithdrawCommand
            {
                AccountId = account.Id,
                Amount = 500, // Ta ut exakt hela saldot
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
        public async Task Handle_WhenWrongOwner_ShouldReturnFailure()
        {
            // Arrange
            var account = CreateTestAccount(ownerId: "user-123");

            var command = new WithdrawCommand
            {
                AccountId = account.Id,
                Amount = 100,
                Description = "Testuttag",
                UserId = "hacker-456" // Fel användare!
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
        public async Task Handle_WhenAccountInactive_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                isActive: false); // Inaktivt konto!

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
        }

        // --------------------------------------------------------
        // Test 6: Konto finns inte
        // --------------------------------------------------------
        [Test]
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

            // Mocken returnerar null = kontot finns inte
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
                Times.Never);
        }
    }
}