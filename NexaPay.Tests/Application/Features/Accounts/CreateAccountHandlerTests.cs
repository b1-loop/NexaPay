// ============================================================
// CreateAccountHandlerTests.cs
// NexaPay.Tests/Application/Features/Accounts
// ============================================================
// Testar CreateAccountHandler med olika scenarier.
//
// Vi testar:
//   1. Lyckad kontoskapelse – konto skapas med rätt värden
//   2. Nytt konto börjar alltid med 0 i saldo
//   3. Nytt konto är alltid aktivt
//   4. SaveChangesAsync anropas exakt en gång
//   5. AddAsync anropas med ett Account-objekt
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Accounts.Commands.CreateAccount;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Accounts
{
    [TestFixture]
    public class CreateAccountHandlerTests : TestBase
    {
        // Handleren vi testar
        private CreateAccountHandler _handler = null!;

        // --------------------------------------------------------
        // Setup – körs innan VARJE enskilt test
        // --------------------------------------------------------
        // [SetUp] = NUnit-attribut som markerar setup-metoden
        // En ny handler skapas för varje test så att
        // ingen state läcker mellan tester
        [SetUp]
        public void Setup()
        {
            // Skapa handleren med våra mock-objekt från TestBase
            // MockUnitOfWork och Mapper ärvs från TestBase
            _handler = new CreateAccountHandler(
                MockUnitOfWork.Object,
                Mapper);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad kontoskapelse
        // --------------------------------------------------------
        [Test]
        public async Task Handle_WhenValidCommand_ShouldCreateAccount()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = "Mitt sparkonto",
                AccountType = AccountType.Savings,
                OwnerId = "user-123"
            };

            // Konfigurera mocken – kontonumret finns inte redan
            // ReturnsAsync(false) = AccountNumberExistsAsync returnerar false
            MockAccountRepository
                .Setup(r => r.AccountNumberExistsAsync(
                    It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act – kör handleren
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "ett giltigt command ska skapa ett konto framgångsrikt");

            result.Value.Should().NotBeNull(
                "ett lyckat resultat ska innehålla en AccountDto");

            result.Value!.AccountName.Should().Be("Mitt sparkonto",
                "kontonamnet ska matcha det vi angav i command");

            result.Value.AccountType.Should().Be("Savings",
                "kontotypen ska mappas från enum till sträng i DTO");

            result.Value.IsActive.Should().BeTrue(
                "ett nytt konto ska alltid vara aktivt");
        }

        // --------------------------------------------------------
        // Test 2: Nytt konto börjar alltid med 0 i saldo
        // --------------------------------------------------------
        [Test]
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
                "ett nytt konto ska ALLTID börja med 0 kr i saldo " +
                "oavsett vad – detta är en kritisk affärsregel");
        }

        // --------------------------------------------------------
        // Test 3: SaveChangesAsync anropas exakt en gång
        // --------------------------------------------------------
        [Test]
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

            // Assert – verifiera att databasen faktiskt uppdaterades
            // Verify kontrollerar att en metod anropades ett visst antal gånger
            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once,
                "SaveChangesAsync ska anropas exakt en gång – " +
                "annars sparas kontot aldrig i databasen");

            // Verifiera att AddAsync anropades med ett Account-objekt
            MockAccountRepository.Verify(
                r => r.AddAsync(
                    It.IsAny<NexaPay.Domain.Entities.Account>()),
                Times.Once,
                "AddAsync ska anropas för att lägga till kontot");
        }

        // --------------------------------------------------------
        // Test 4: Olika kontotyper
        // --------------------------------------------------------
        [TestCase(AccountType.Checking, "Checking")]
        [TestCase(AccountType.Savings, "Savings")]
        [TestCase(AccountType.ISK, "ISK")]
        // [TestCase] låter oss köra samma test med olika parametrar
        // Istället för tre separata tester kör vi ett test tre gånger
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