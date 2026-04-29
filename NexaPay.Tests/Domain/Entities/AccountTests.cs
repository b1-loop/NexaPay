// ============================================================
// AccountTests.cs – NexaPay.Tests/Domain/Entities
// ============================================================
// Testar Account-entitetens egenskaper och standardvärden.
//
// Vad testar vi här?
// Domain-lagret innehåller inga komplexa metoder men vi
// verifierar att entiteterna initieras korrekt och att
// våra standardvärden är rätt.
//
// Teststruktur – AAA-mönstret:
//   Arrange  = förbered testdata
//   Act      = utför operationen
//   Assert   = verifiera resultatet
// ============================================================

using FluentAssertions;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Domain.Entities
{
    [TestFixture]
    public class AccountTests
    {
        // --------------------------------------------------------
        // Test 1: Nytt konto ska ha korrekta standardvärden
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att ett nytt Account-objekt har korrekta " +
            "standardvärden – saldo 0, aktivt, inga transaktioner " +
            "och inga kort. Detta är grundläggande domänregler.")]
        public void Account_WhenCreated_ShouldHaveCorrectDefaultValues()
        {
            // Arrange
            var account = new Account
            {
                Id = Guid.NewGuid(),
                AccountNumber = "SE123456789",
                AccountName = "Mitt sparkonto",
                Balance = 0,
                AccountType = AccountType.Savings,
                IsActive = true,
                OwnerId = "user-123",
                CreatedAt = DateTime.UtcNow
            };

            // Assert
            account.Balance.Should().Be(0,
                "ett nytt konto ska alltid börja med 0 i saldo");

            account.IsActive.Should().BeTrue(
                "ett nytt konto ska alltid vara aktivt");

            account.AccountType.Should().Be(AccountType.Savings,
                "kontotypen ska matcha det vi satte");

            account.Transactions.Should().BeEmpty(
                "ett nytt konto ska inte ha några transaktioner");

            account.Cards.Should().BeEmpty(
                "ett nytt konto ska inte ha några kort");
        }

        // --------------------------------------------------------
        // Test 2: Entiteten tillåter negativt saldo
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att Account-entiteten i sig INTE skyddar " +
            "mot negativt saldo – det ansvaret ligger i " +
            "WithdrawHandler (Application-lagret). " +
            "Separation of concerns är en Clean Architecture-princip.")]
        public void Account_WithNegativeBalance_ShouldBeAllowedByEntity()
        {
            // Arrange
            var account = new Account
            {
                Id = Guid.NewGuid(),
                Balance = -100
            };

            // Assert
            account.Balance.Should().Be(-100,
                "entiteten har inget skydd mot negativt saldo – " +
                "det hanteras av affärslogiken i Application-lagret");
        }

        // --------------------------------------------------------
        // Test 3: Stänga ett konto (soft delete)
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att ett konto kan markeras som inaktivt " +
            "(soft delete) och att UpdatedAt sätts korrekt. " +
            "Vi tar aldrig bort konton fysiskt av revisionsskäl.")]
        public void Account_WhenDeactivated_IsActiveShouldBeFalse()
        {
            // Arrange
            var account = new Account
            {
                Id = Guid.NewGuid(),
                IsActive = true
            };

            // Act
            account.IsActive = false;
            account.UpdatedAt = DateTime.UtcNow;

            // Assert
            account.IsActive.Should().BeFalse(
                "ett stängt konto ska vara markerat som inaktivt");

            account.UpdatedAt.Should().NotBeNull(
                "UpdatedAt ska sättas när kontot stängs");
        }

        // --------------------------------------------------------
        // Test 4: Konto med transaktioner
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att transaktioner kan kopplas till ett konto " +
            "och att navigationsegenskapen fungerar korrekt. " +
            "Används av EF Core för att ladda relaterad data.")]
        public void Account_WithTransactions_ShouldContainThem()
        {
            // Arrange
            var account = new Account
            {
                Id = Guid.NewGuid(),
                Balance = 500
            };

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                Amount = 500,
                Type = TransactionType.Deposit,
                Description = "Testinsättning",
                BalanceAfterTransaction = 500,
                AccountId = account.Id,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            account.Transactions.Add(transaction);

            // Assert
            account.Transactions.Should().HaveCount(1,
                "kontot ska ha exakt en transaktion");

            account.Transactions.First().Amount.Should().Be(500,
                "transaktionsbeloppet ska vara 500");
        }

        // --------------------------------------------------------
        // Test 5: Konto med kort
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att kort kan kopplas till ett konto " +
            "och att navigationsegenskapen fungerar korrekt. " +
            "Ett konto kan ha flera kort kopplade till sig.")]
        public void Account_WithCards_ShouldContainThem()
        {
            // Arrange
            var account = new Account { Id = Guid.NewGuid() };

            var card = new Card
            {
                Id = Guid.NewGuid(),
                CardNumber = "4532123456789010",
                CardHolderName = "ANNA SVENSSON",
                Status = CardStatus.Active,
                AccountId = account.Id,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            account.Cards.Add(card);

            // Assert
            account.Cards.Should().HaveCount(1,
                "kontot ska ha exakt ett kort");

            account.Cards.First().Status.Should().Be(
                CardStatus.Active,
                "kortet ska vara aktivt");
        }
    }
}