// ============================================================
// Money.cs – NexaPay.Domain/ValueObjects
// ============================================================
// Värdeobjekt som representerar ett penningbelopp i en valuta.
//
// Varför ett eget Money istället för 'decimal'?
//   - Tvingar in valutainformationen tillsammans med beloppet
//     så att vi aldrig av misstag kan blanda SEK och EUR.
//   - Inkapslar regler som "aldrig negativt belopp" och
//     "alltid avrundat till 2 decimaler" på ETT ställe.
//   - Är immutabel – nya instanser skapas av operator + och -,
//     gamla muteras aldrig.
//
// EF Core mappar Money som "owned type" – det blir två kolumner
// (Amount + Currency) på samma rad som ägar-entiteten.
// ============================================================

using System.Text.Json.Serialization;
using NexaPay.Domain.Enums;

namespace NexaPay.Domain.ValueObjects
{
    public sealed class Money : IEquatable<Money>
    {
        // Parameterlös ctor krävs av EF Core när materialiserar
        // owned-instansen från databasen. Aldrig anropad av domänen.
        private Money() { }

        public decimal Amount { get; private set; }
        public Currency Currency { get; private set; }

        // [JsonConstructor] så outbox-dispatchern kan rekonstruera
        // Money-instanser från serialiserad payload.
        [JsonConstructor]
        public Money(decimal amount, Currency currency)
        {
            // Domäninvariant: ett konto kan ha 0 men aldrig negativt belopp.
            // Försök till uttag som skulle resultera i minus kontrolleras
            // separat i Account.Withdraw() innan en ny Money byggs.
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Belopp kan inte vara negativt");

            // Banker arbetar i ören – avrunda alltid till 2 decimaler med
            // "halv från noll" (matematiskt vanligast i banksammanhang).
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            Currency = currency;
        }

        // Praktisk fabriksmetod – ett nollbelopp i given valuta.
        public static Money Zero(Currency currency) => new(0m, currency);

        // Operatorerna nedan bygger nya Money-instanser så att vi kan
        // skriva 'Balance = Balance + amount' i Account-aggregatet.
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

        // Vakt mot misstag som "SEK + EUR". Kastar tidigt och tydligt
        // istället för att tyst räkna fel.
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
