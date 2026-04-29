// ============================================================
// TransferHandlerTests.cs
// NexaPay.Tests/Application/Features/Transactions
// ============================================================
// Testar TransferHandler – den mest komplexa operationen.
//
// Transfer är kritisk eftersom den involverar:
//   - Två konton som måste uppdateras atomärt
//   - Overdraft-skydd på avsändarkontot
//   - Ägarskapsverifiering
//   - Två transaktionsposter som skapas
//
// Vi testar:
//   1. Lyckad överföring – båda saldona uppdateras korrekt
//   2. Fel ägare – returnerar Failure utan att spara
//   3. Otillräckligt saldo – overdraft-skydd fungerar
//   4. Avsändarkonto finns inte
//   5. Mottagarkonto finns inte
//   6. Avsändarkonto är inaktivt
//   7. Mottagarkonto är inaktivt
//   8. Exakt saldo – överför hela beloppet
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Transactions.Commands.Transfer;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Transactions
{
    [TestFixture]
    [Category("Application")]
    [Category("Transactions")]
    [Category("Transfer")]
    public class TransferHandlerTests : TestBase
    {
        private TransferHandler _handler = null!;

        // --------------------------------------------------------
        // Setup – körs innan VARJE test
        // --------------------------------------------------------
        [SetUp]
        public void Setup()
        {
            // Återställ alla mocks innan varje test
            // Reset() garanterar att varje test börjar rent
            // utan påverkan från tidigare tester
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

            // Skapa en ny handler för varje test
            _handler = new TransferHandler(
                MockUnitOfWork.Object,
                Mapper);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad överföring
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att en giltig överföring uppdaterar " +
            "båda kontosaldona korrekt och att en TransactionDto " +
            "returneras. T.ex. från saldo 1000 - 300 = 700, " +
            "till saldo 500 + 300 = 800.")]
        public async Task Handle_WhenValidTransfer_ShouldUpdateBothBalances()
        {
            // Arrange
            var userId = "user-123";

            // Skapa avsändarkontot med 1000 kr
            var fromAccount = CreateTestAccount(
                ownerId: userId,
                balance: 1000);

            // Skapa mottagarkontot med 500 kr
            // OwnerId kan vara vem som helst – man kan
            // överföra till andras konton
            var toAccount = CreateTestAccount(
                ownerId: "other-user",
                balance: 500);

            var command = new TransferCommand
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                Amount = 300, // Överför 300 kr
                Description = "Testöverföring",
                UserId = userId
            };

            // Konfigurera mocken för båda kontona
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(fromAccount.Id))
                .ReturnsAsync(fromAccount);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(toAccount.Id))
                .ReturnsAsync(toAccount);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "en giltig överföring ska lyckas");

            result.Value.Should().NotBeNull(
                "ett lyckat resultat ska innehålla en TransactionDto");

            result.Value!.Amount.Should().Be(300,
                "transaktionsbeloppet ska vara 300");

            result.Value.Type.Should().Be("Transfer",
                "transaktionstypen ska vara Transfer");

            // Verifiera att avsändarens saldo minskade
            fromAccount.Balance.Should().Be(700,
                "avsändarens saldo ska vara 1000 - 300 = 700");

            // Verifiera att mottagarens saldo ökade
            toAccount.Balance.Should().Be(800,
                "mottagarens saldo ska vara 500 + 300 = 800");

            // Verifiera att SaveChangesAsync anropades exakt en gång
            // Unit of Work sparar ALLT i en enda transaktion
            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once,
                "SaveChangesAsync ska anropas exakt en gång – " +
                "allt sparas atomärt i en databastransaktion");

            // Verifiera att TWÅ transaktionsposter skapades
            // En för avsändaren och en för mottagaren
            MockTransactionRepository.Verify(
                t => t.AddAsync(
                    It.IsAny<NexaPay.Domain.Entities.Transaction>()),
                Times.Exactly(2),
                "Två transaktionsposter ska skapas – " +
                "en för avsändaren och en för mottagaren");
        }

        // --------------------------------------------------------
        // Test 2: Fel ägare
        // --------------------------------------------------------
        [Test]
        [Category("Security")]
        [Description(
            "Verifierar att en användare INTE kan överföra pengar " +
            "från ett konto som tillhör någon annan. " +
            "OwnerId måste matcha inloggad UserId. " +
            "SaveChangesAsync ska ALDRIG anropas i detta fall.")]
        public async Task Handle_WhenWrongOwner_ShouldReturnFailure()
        {
            // Arrange
            // Kontot tillhör user-123 men hacker-456 försöker använda det
            var fromAccount = CreateTestAccount(ownerId: "user-123");
            var toAccount = CreateTestAccount(ownerId: "someone-else");

            var command = new TransferCommand
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                Amount = 100,
                Description = "Hackförsök",
                UserId = "hacker-456" // Fel användare!
            };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(fromAccount.Id))
                .ReturnsAsync(fromAccount);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(toAccount.Id))
                .ReturnsAsync(toAccount);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "en användare ska inte kunna överföra från " +
                "någon annans konto");

            // KRITISKT: Inget ska sparas vid fel ägare
            // Detta verifierar att buggen är fixad!
            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "SaveChangesAsync ska INTE anropas vid fel ägare – " +
                "detta verifierar att buggen är fixad!");
        }

        // --------------------------------------------------------
        // Test 3: Otillräckligt saldo – overdraft-skydd
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Category("Overdraft")]
        [Description(
            "Verifierar overdraft-skyddet för överföringar. " +
            "Man ska inte kunna överföra mer än vad som finns. " +
            "Varken avsändarens eller mottagarens saldo " +
            "ska ändras om saldot inte räcker.")]
        public async Task Handle_WhenInsufficientBalance_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var fromAccount = CreateTestAccount(
                ownerId: userId,
                balance: 100); // Bara 100 kr

            var toAccount = CreateTestAccount(
                ownerId: "other-user",
                balance: 500);

            var command = new TransferCommand
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                Amount = 500, // Försöker överföra 500 kr!
                Description = "För stor överföring",
                UserId = userId
            };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(fromAccount.Id))
                .ReturnsAsync(fromAccount);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(toAccount.Id))
                .ReturnsAsync(toAccount);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "överföring ska misslyckas om saldot inte räcker");

            result.Error.Should().Contain("saldo",
                "felmeddelandet ska nämna saldot");

            // Saldona ska vara oförändrade
            fromAccount.Balance.Should().Be(100,
                "avsändarens saldo ska vara oförändrat");

            toAccount.Balance.Should().Be(500,
                "mottagarens saldo ska vara oförändrat");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas när saldot inte räcker");
        }

        // --------------------------------------------------------
        // Test 4: Avsändarkonto finns inte
        // --------------------------------------------------------
        [Test]
        [Category("NotFound")]
        [Description(
            "Verifierar att överföring misslyckas när " +
            "avsändarkontot inte existerar i databasen.")]
        public async Task Handle_WhenFromAccountNotFound_ShouldReturnFailure()
        {
            // Arrange
            var command = new TransferCommand
            {
                FromAccountId = Guid.NewGuid(), // Finns inte!
                ToAccountId = Guid.NewGuid(),
                Amount = 100,
                Description = "Test",
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
                "överföring ska misslyckas om avsändarkontot inte finns");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 5: Mottagarkonto finns inte
        // --------------------------------------------------------
        [Test]
        [Category("NotFound")]
        [Description(
            "Verifierar att överföring misslyckas när " +
            "mottagarkontot inte existerar i databasen.")]
        public async Task Handle_WhenToAccountNotFound_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var fromAccount = CreateTestAccount(
                ownerId: userId,
                balance: 1000);

            var command = new TransferCommand
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = Guid.NewGuid(), // Mottagaren finns inte!
                Amount = 100,
                Description = "Test",
                UserId = userId
            };

            // Avsändarkontot finns
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(fromAccount.Id))
                .ReturnsAsync(fromAccount);

            // Mottagarkontot finns INTE
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(
                    It.Is<Guid>(id => id != fromAccount.Id)))
                .ReturnsAsync(
                    (NexaPay.Domain.Entities.Account?)null);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "överföring ska misslyckas om mottagarkontot inte finns");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 6: Avsändarkonto är inaktivt
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att överföring nekas om avsändarkontot " +
            "är inaktivt. Man kan inte överföra från ett stängt konto.")]
        public async Task Handle_WhenFromAccountInactive_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var fromAccount = CreateTestAccount(
                ownerId: userId,
                balance: 1000,
                isActive: false); // Inaktivt!

            var toAccount = CreateTestAccount(
                ownerId: "other-user");

            var command = new TransferCommand
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                Amount = 100,
                Description = "Test",
                UserId = userId
            };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(fromAccount.Id))
                .ReturnsAsync(fromAccount);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(toAccount.Id))
                .ReturnsAsync(toAccount);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "överföring ska nekas från ett inaktivt konto");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas för ett inaktivt konto");
        }

        // --------------------------------------------------------
        // Test 7: Mottagarkonto är inaktivt
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att överföring nekas om mottagarkontot " +
            "är inaktivt. Man kan inte överföra till ett stängt konto.")]
        public async Task Handle_WhenToAccountInactive_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var fromAccount = CreateTestAccount(
                ownerId: userId,
                balance: 1000);

            var toAccount = CreateTestAccount(
                ownerId: "other-user",
                isActive: false); // Mottagaren är inaktiv!

            var command = new TransferCommand
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                Amount = 100,
                Description = "Test",
                UserId = userId
            };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(fromAccount.Id))
                .ReturnsAsync(fromAccount);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(toAccount.Id))
                .ReturnsAsync(toAccount);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "överföring ska nekas till ett inaktivt konto");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "inget ska sparas om mottagarkontot är inaktivt");
        }

        // --------------------------------------------------------
        // Test 8: Exakt saldo – överför hela beloppet
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Category("EdgeCase")]
        [Description(
            "Verifierar gränsfallet där Amount == Balance. " +
            "Man ska kunna överföra exakt hela saldot. " +
            "Avsändarens saldo ska bli 0 efter överföringen.")]
        public async Task Handle_WhenTransferringExactBalance_ShouldSucceed()
        {
            // Arrange
            var userId = "user-123";
            var fromAccount = CreateTestAccount(
                ownerId: userId,
                balance: 500); // Exakt 500 kr

            var toAccount = CreateTestAccount(
                ownerId: "other-user",
                balance: 0);

            var command = new TransferCommand
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                Amount = 500, // Överför exakt hela saldot
                Description = "Tömmer kontot",
                UserId = userId
            };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(fromAccount.Id))
                .ReturnsAsync(fromAccount);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(toAccount.Id))
                .ReturnsAsync(toAccount);

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "man ska kunna överföra exakt hela saldot");

            fromAccount.Balance.Should().Be(0,
                "avsändarens saldo ska vara 0 efter överföringen");

            toAccount.Balance.Should().Be(500,
                "mottagarens saldo ska vara 500 efter överföringen");
        }
    }
}