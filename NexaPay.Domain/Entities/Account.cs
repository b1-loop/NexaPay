using NexaPay.Domain.Enums;
using NexaPay.Domain.Events;
using NexaPay.Domain.ValueObjects;

namespace NexaPay.Domain.Entities
{
    public class Account : BaseEntity
    {
        private Account() { }

        public string AccountNumber { get; private set; } = string.Empty;
        public string AccountName { get; private set; } = string.Empty;
        public Money Balance { get; private set; } = Money.Zero(Currency.SEK);
        public AccountType AccountType { get; private set; }
        public AccountStatus Status { get; private set; } = AccountStatus.Open;
        public string OwnerId { get; private set; } = string.Empty;

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<Card> Cards { get; set; } = new List<Card>();
        public byte[] RowVersion { get; set; } = [];

        public static Account Open(
            string accountNumber,
            string accountName,
            AccountType accountType,
            string ownerId,
            Currency currency = Currency.SEK)
        {
            return new Account
            {
                Id = Guid.NewGuid(),
                AccountNumber = accountNumber,
                AccountName = accountName,
                AccountType = accountType,
                OwnerId = ownerId,
                Balance = Money.Zero(currency),
                CreatedAt = DateTime.UtcNow
            };
        }

        public Transaction Deposit(Money amount, string description, Guid? idempotencyKey = null)
        {
            if (Status != AccountStatus.Open)
                throw new InvalidOperationException(
                    $"Kan inte sätta in pengar på ett {Status.ToString().ToLower()} konto");

            Balance = Balance + amount;
            UpdatedAt = DateTime.UtcNow;

            RaiseDomainEvent(new MoneyDeposited(Id, OwnerId, amount, Balance, DateTime.UtcNow));

            return new Transaction
            {
                Id = Guid.NewGuid(),
                Amount = amount,
                Type = TransactionType.Deposit,
                Description = description,
                BalanceAfterTransaction = Balance,
                AccountId = Id,
                IdempotencyKey = idempotencyKey,
                CreatedAt = DateTime.UtcNow
            };
        }

        public Transaction Withdraw(Money amount, string description, Guid? idempotencyKey = null)
        {
            if (Status != AccountStatus.Open)
                throw new InvalidOperationException(
                    $"Kan inte ta ut pengar från ett {Status.ToString().ToLower()} konto");

            if (Balance < amount)
                throw new InvalidOperationException(
                    $"Otillräckligt saldo. Tillgängligt saldo: {Balance}, Begärt belopp: {amount}");

            Balance = Balance - amount;
            UpdatedAt = DateTime.UtcNow;

            RaiseDomainEvent(new MoneyWithdrawn(Id, OwnerId, amount, Balance, DateTime.UtcNow));

            return new Transaction
            {
                Id = Guid.NewGuid(),
                Amount = amount,
                Type = TransactionType.Withdrawal,
                Description = description,
                BalanceAfterTransaction = Balance,
                AccountId = Id,
                IdempotencyKey = idempotencyKey,
                CreatedAt = DateTime.UtcNow
            };
        }

        public (Transaction FromTransaction, Transaction ToTransaction) TransferTo(
            Money amount,
            string description,
            Account receiver,
            Guid? idempotencyKey = null)
        {
            if (Status != AccountStatus.Open)
                throw new InvalidOperationException($"Avsändarkontot är {Status.ToString().ToLower()}");

            if (receiver.Status != AccountStatus.Open)
                throw new InvalidOperationException($"Mottagarkontot är {receiver.Status.ToString().ToLower()}");

            if (Balance < amount)
                throw new InvalidOperationException(
                    $"Otillräckligt saldo. Tillgängligt: {Balance}, Begärt: {amount}");

            Balance = Balance - amount;
            UpdatedAt = DateTime.UtcNow;

            receiver.Balance = receiver.Balance + amount;
            receiver.UpdatedAt = DateTime.UtcNow;

            RaiseDomainEvent(new MoneyTransferred(Id, receiver.Id, OwnerId, amount, DateTime.UtcNow));

            var fromTransaction = new Transaction
            {
                Id = Guid.NewGuid(),
                Amount = amount,
                Type = TransactionType.Transfer,
                Description = $"Överföring till konto: {description}",
                BalanceAfterTransaction = Balance,
                ReceiverAccountId = receiver.Id,
                AccountId = Id,
                IdempotencyKey = idempotencyKey,
                CreatedAt = DateTime.UtcNow
            };

            var toTransaction = new Transaction
            {
                Id = Guid.NewGuid(),
                Amount = amount,
                Type = TransactionType.Transfer,
                Description = $"Överföring från konto: {description}",
                BalanceAfterTransaction = receiver.Balance,
                AccountId = receiver.Id,
                CreatedAt = DateTime.UtcNow
            };

            return (fromTransaction, toTransaction);
        }

        public void Freeze()
        {
            if (Status == AccountStatus.Closed)
                throw new InvalidOperationException("Kan inte frysa ett stängt konto");
            if (Status == AccountStatus.Frozen)
                throw new InvalidOperationException("Kontot är redan fryst");

            Status = AccountStatus.Frozen;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Unfreeze()
        {
            if (Status != AccountStatus.Frozen)
                throw new InvalidOperationException("Kontot är inte fryst");

            Status = AccountStatus.Open;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Close()
        {
            if (Status == AccountStatus.Closed)
                throw new InvalidOperationException("Kontot är redan stängt");

            if (Balance.Amount > 0)
                throw new InvalidOperationException(
                    $"Kontot kan inte stängas eftersom det har ett saldo på {Balance}. " +
                    "Töm kontot innan du stänger det.");

            Status = AccountStatus.Closed;
            UpdatedAt = DateTime.UtcNow;

            RaiseDomainEvent(new AccountClosed(Id, OwnerId, DateTime.UtcNow));
        }
    }
}
