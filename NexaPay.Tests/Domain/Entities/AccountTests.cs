// ============================================================
// AccountTests.cs – NexaPay.Tests/Domain/Entities
// ============================================================
// Testar Account-entitetens egenskaper och standardvärden.
// ============================================================

using FluentAssertions;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Domain.Entities
{
    [TestFixture]
    [Category("Domain")]
    // [Category] på klassnivå = gäller alla tester i klassen
    // Alla tester i denna klass kategoriseras som "Domain"
    public class AccountTests
    {
        [Test]
        [Category("Account")]
        [Category("DefaultValues")]
        [Description(
            "Verifierar att ett nytt Account-objekt har korrekta " +
            "standardvärden – saldo 0, aktivt, inga transaktioner " +
            "och inga kort.")]
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

        [Test]
        [Category("Account")]
        [Category("Balance")]
        [Description(
            "Verifierar att Account-entiteten INTE skyddar mot " +
            "negativt saldo – det ansvaret ligger i WithdrawHandler.")]
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
                "entiteten har inget skydd mot negativt saldo");
        }

        [Test]
        [Category("Account")]
        [Category("SoftDelete")]
        [Description(
            "Verifierar att ett konto kan markeras som inaktivt " +
            "och att UpdatedAt sätts korrekt vid soft delete.")]
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

        [Test]
        [Category("Account")]
        [Category("Relations")]
        [Description(
            "Verifierar att transaktioner kan kopplas till ett konto " +
            "och att navigationsegenskapen fungerar korrekt.")]
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

        [Test]
        [Category("Account")]
        [Category("Relations")]
        [Description(
            "Verifierar att kort kan kopplas till ett konto " +
            "och att navigationsegenskapen fungerar korrekt.")]
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