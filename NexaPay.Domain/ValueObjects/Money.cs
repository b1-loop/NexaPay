using NexaPay.Domain.Enums;

namespace NexaPay.Domain.ValueObjects
{
    public sealed class Money : IEquatable<Money>
    {
        private Money() { }  // For EF Core

        public decimal Amount { get; private set; }
        public Currency Currency { get; private set; }

        public Money(decimal amount, Currency currency)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Belopp kan inte vara negativt");
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            Currency = currency;
        }

        public static Money Zero(Currency currency) => new(0m, currency);

        public static Money operator +(Money a, Money b)
        {
            EnforceSameCurrency(a, b);
            return new Money(a.Amount + b.Amount, a.Currency);
        }

        public static Money operator -(Money a, Money b)
        {
            EnforceSameCurrency(a, b);
            return new Money(a.Amount - b.Amount, a.Currency);
        }

        public static bool operator >(Money a, Money b)  { EnforceSameCurrency(a, b); return a.Amount > b.Amount; }
        public static bool operator <(Money a, Money b)  { EnforceSameCurrency(a, b); return a.Amount < b.Amount; }
        public static bool operator >=(Money a, Money b) { EnforceSameCurrency(a, b); return a.Amount >= b.Amount; }
        public static bool operator <=(Money a, Money b) { EnforceSameCurrency(a, b); return a.Amount <= b.Amount; }

        private static void EnforceSameCurrency(Money a, Money b)
        {
            if (a.Currency != b.Currency)
                throw new InvalidOperationException(
                    $"Kan inte blanda valutor: {a.Currency} och {b.Currency}");
        }

        public bool Equals(Money? other) =>
            other is not null && Amount == other.Amount && Currency == other.Currency;

        public override bool Equals(object? obj) => Equals(obj as Money);
        public override int GetHashCode() => HashCode.Combine(Amount, Currency);
        public static bool operator ==(Money? a, Money? b) => Equals(a, b);
        public static bool operator !=(Money? a, Money? b) => !Equals(a, b);
        public override string ToString() => $"{Amount:F2} {Currency}";
    }
}
