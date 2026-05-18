// ============================================================
// AccountTests.cs – NexaPay.Tests/Domain/Entities
// ============================================================
// Domän-tester för Account-aggregatet. Verifierar invarianter
// (negativt saldo, fryst konto, etc.) direkt mot aggregatet
// utan något ramverk – inga mocks, inga repositories.
// ============================================================

using FluentAssertions;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NexaPay.Domain.ValueObjects;
using NUnit.Framework;

namespace NexaPay.Tests.Domain.Entities
{
    [TestFixture]
    [Category("Domain")]
    public class AccountTests
    {
        private static Money Sek(decimal amount) => new(amount, Currency.SEK);

        [Test]
        [Category("Account")]
        public void Account_WhenOpened_ShouldHaveCorrectDefaultValues()
        {
            var account = Account.Open("SE1", "Konto", AccountType.Savings, "user-1");

            account.Balance.Amount.Should().Be(0);
            account.Balance.Currency.Should().Be(Currency.SEK);
            account.Status.Should().Be(AccountStatus.Open);
            account.AccountType.Should().Be(AccountType.Savings);
            account.Transactions.Should().BeEmpty();
            account.Cards.Should().BeEmpty();
        }

        [Test]
        [Category("Account")]
        public void Account_Deposit_ShouldIncreaseBalance()
        {
            var account = Account.Open("SE2", "Konto", AccountType.Checking, "user-1");

            var tx = account.Deposit(Sek(500), "Testinsättning");

            account.Balance.Amount.Should().Be(500);
            account.Balance.Currency.Should().Be(Currency.SEK);
            tx.Amount.Amount.Should().Be(500);
            tx.Type.Should().Be(TransactionType.Deposit);
            tx.BalanceAfterTransaction.Amount.Should().Be(500);
        }

        [Test]
        [Category("Account")]
        public void Account_Withdraw_ShouldDecreaseBalance()
        {
            var account = Account.Open("SE3", "Konto", AccountType.Checking, "user-1");
            account.Deposit(Sek(1000), "Initial");

            var tx = account.Withdraw(Sek(400), "Uttag");

            account.Balance.Amount.Should().Be(600);
            tx.Type.Should().Be(TransactionType.Withdrawal);
        }

        [Test]
        [Category("Account")]
        public void Account_Withdraw_WhenInsufficientBalance_ShouldThrow()
        {
            var account = Account.Open("SE4", "Konto", AccountType.Checking, "user-1");
            account.Deposit(Sek(100), "Initial");

            var act = () => account.Withdraw(Sek(200), "För stort uttag");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Otillräckligt saldo*");
        }

        [Test]
        [Category("Account")]
        public void Account_Deposit_WhenCurrencyMismatch_ShouldThrow()
        {
            var account = Account.Open("SE5", "Konto", AccountType.Checking, "user-1", Currency.SEK);

            var act = () => account.Deposit(new Money(100, Currency.EUR), "EUR-insättning");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*valutor*");
        }

        [Test]
        [Category("Account")]
        public void Account_Close_ShouldSetIsActiveToFalse()
        {
            var account = Account.Open("SE6", "Konto", AccountType.Checking, "user-1");

            account.Close();

            account.Status.Should().Be(AccountStatus.Closed);
            account.UpdatedAt.Should().NotBeNull();
        }

        [Test]
        [Category("Account")]
        public void Account_Close_WhenBalanceGreaterThanZero_ShouldThrow()
        {
            var account = Account.Open("SE7", "Konto", AccountType.Checking, "user-1");
            account.Deposit(Sek(100), "Initial");

            var act = () => account.Close();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*saldo*");
        }

        [Test]
        [Category("Account")]
        public void Account_WithCards_ShouldContainThem()
        {
            var account = Account.Open("SE8", "Konto", AccountType.Checking, "user-1");

            var card = new Card
            {
                Id = Guid.NewGuid(),
                CardToken = Guid.NewGuid().ToString(),
                Last4Digits = "9010",
                CardHolderName = "ANNA SVENSSON",
                AccountId = account.Id,
                CreatedAt = DateTime.UtcNow
            };
            card.Activate();
            account.Cards.Add(card);

            account.Cards.Should().HaveCount(1);
            account.Cards.First().Status.Should().Be(CardStatus.Active);
        }
    }
}
