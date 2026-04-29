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
//   5. Transaktionspost skapas med rätt värden
//
// Varför mockar vi IUnitOfWork?
// Vi vill inte prata med en riktig databas i unit-tester.
// Mocken låter oss kontrollera exakt vad som returneras
// och verifiera att rätt metoder anropades.
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
        // Handleren vi testar
        private DepositHandler _handler = null!;

        // [SetUp] körs innan VARJE test
        [SetUp]
        public void Setup()
        {
            _handler = new DepositHandler(
                MockUnitOfWork.Object,
                Mapper);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad insättning
        // --------------------------------------------------------
        [Test]
        public async Task Handle_WhenValidDeposit_ShouldIncreaseBalance()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                balance: 1000); // Börjar med 1000 kr

            var command = new DepositCommand
            {
                AccountId = account.Id,
                Amount = 500, // Sätter in 500 kr
                Description = "Testinsättning",
                UserId = userId
            };

            // Konfigurera mocken att returnera vårt testkonto
            // när GetByIdAsync anropas med rätt ID
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id))
                .ReturnsAsync(account);

            // Act – kör handleren
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "en giltig insättning ska lyckas");

            result.Value.Should().NotBeNull(
                "ett lyckat resultat ska innehålla en TransactionDto");

            result.Value!.Amount.Should().Be(500,
                "transaktionsbeloppet ska matcha insättningen");

            result.Value.BalanceAfterTransaction.Should().Be(1500,
                "saldot ska vara 1000 + 500 = 1500 efter insättningen");

            // Verifiera att SaveChangesAsync anropades
            // Om detta inte anropades sparades ingenting i databasen!
            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once,
                "SaveChangesAsync ska anropas exakt en gång");
        }

        // --------------------------------------------------------
        // Test 2: Konto finns inte
        // --------------------------------------------------------
        [Test]
        public async Task Handle_WhenAccountNotFound_ShouldReturnFailure()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(), // Slumpmässigt ID som inte finns
                Amount = 500,
                Description = "Testinsättning",
                UserId = "user-123"
            };

            // Konfigurera mocken att returnera null
            // = kontot finns inte i databasen
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Account?)null);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "om kontot inte finns ska operationen misslyckas");

            result.Error.Should().NotBeEmpty(
                "ett felmeddelande ska finnas");

            // Verifiera att SaveChangesAsync INTE anropades
            // Inget ska sparas om kontot inte hittades
            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas om kontot inte hittades");
        }

        // --------------------------------------------------------
        // Test 3: Fel ägare
        // --------------------------------------------------------
        [Test]
        public async Task Handle_WhenWrongOwner_ShouldReturnFailure()
        {
            // Arrange
            var account = CreateTestAccount(ownerId: "user-123");

            var command = new DepositCommand
            {
                AccountId = account.Id,
                Amount = 500,
                Description = "Testinsättning",
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
        public async Task Handle_WhenAccountInactive_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                isActive: false); // Inaktivt konto!

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
        }

        // --------------------------------------------------------
        // Test 5: Transaktionspost skapas med rätt typ
        // --------------------------------------------------------
        [Test]
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

            // TransactionType mappas till sträng i DTO
            result.Value!.Type.Should().Be("Deposit",
                "transaktionstypen ska vara Deposit för insättningar");

            result.Value.Description.Should().Be("Löneinsättning",
                "beskrivningen ska matcha det vi angav");
        }
    }
}