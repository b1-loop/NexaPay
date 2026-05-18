// ============================================================
// Account.cs – NexaPay.Domain/Entities
// ============================================================
// Aggregat-rot för ett bankkonto i NexaPay. Detta är hjärtat i
// hela domänen – all pengar-rörelse går genom metoder på denna
// klass så att domäninvarianter alltid hålls:
//
//   * Saldo kan aldrig bli negativt (kontrolleras före varje uttag).
//   * Frysta/stängda konton kan inte ta emot transaktioner.
//   * Stängda konton måste först ha 0 i saldo.
//   * Domain Events raises på alla värdetransaktioner så att
//     notifikations- och audit-handlers kan reagera utanför
//     aggregatet (Open/Closed Principle).
//
// Alla setters är PRIVATA – ingen extern kod kan ändra saldo,
// status eller ägare direkt. Hela "rörlig logik" sker via
// intention-revealing metoder: Open, Deposit, Withdraw, Transfer,
// PayInvoice, Freeze, Unfreeze, Close.
// ============================================================

using NexaPay.Domain.Enums;
using NexaPay.Domain.Events;
using NexaPay.Domain.ValueObjects;

namespace NexaPay.Domain.Entities
{
    public class Account : BaseEntity
    {
        // Parameterlös konstruktor krävs av EF Core vid materialisering
        // från databasen. Är PRIVAT så att domänen tvingas använda
        // fabriksmetoden Open() för att skapa nya konton.
        private Account() { }

        public string AccountNumber { get; private set; } = string.Empty;
        public string AccountName { get; private set; } = string.Empty;
        public Money Balance { get; private set; } = Money.Zero(Currency.SEK);
        public AccountType AccountType { get; private set; }
        public AccountStatus Status { get; private set; } = AccountStatus.Open;
        public string OwnerId { get; private set; } = string.Empty;

        // Navigation properties som EF Core fyller via include/lazy-load.
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<Card> Cards { get; set; } = new List<Card>();

        // Optimistisk samtidighetskontroll – SQL Server uppdaterar
        // RowVersion automatiskt vid varje UPDATE. Om två requests
        // försöker spara samtidigt får den sista en DbUpdateConcurrencyException,
        // som ConcurrencyRetryBehavior fångar och försöker igen.
        public byte[] RowVersion { get; set; } = [];

        // Fabriksmetod för att skapa ett helt nytt konto. Ger oss en
        // tydlig "ingång" och garanterar att Id sätts och Balance
        // initieras till Money.Zero i rätt valuta.
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

        public Transaction PayInvoice(
            Money amount,
            string bankgiro,
            string ocr,
            string description,
            Guid? idempotencyKey = null)
        {
            if (Status != AccountStatus.Open)
                throw new InvalidOperationException(
                    $"Kan inte betala faktura från ett {Status.ToString().ToLower()} konto");

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
                Type = TransactionType.InvoicePayment,
                Description = description,
                BalanceAfterTransaction = Balance,
                AccountId = Id,
                Bankgiro = bankgiro,
                Ocr = ocr,
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

            // Egen Money-instans per transaktion – Money är en EF Core owned type
            // och en owned-instans kan inte delas mellan två ägar-entiteter.
            var fromTransaction = new Transaction
            {
                Id = Guid.NewGuid(),
                Amount = new Money(amount.Amount, amount.Currency),
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
                Amount = new Money(amount.Amount, amount.Currency),
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
