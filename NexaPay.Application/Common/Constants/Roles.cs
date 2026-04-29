// ============================================================
// Roles.cs – NexaPay.Application/Common/Constants
// ============================================================
// Definierar alla roller i NexaPay-systemet som konstanter.
//
// Varför konstanter och inte enum?
// ASP.NET Core's [Authorize(Roles = "...")] kräver strängar.
// Genom att använda konstanter undviker vi .ToString() överallt
// och får compile-time kontroll på rollnamnen.
//
// Användning i controllers:
//   [Authorize(Roles = Roles.Admin)]
//   [Authorize(Roles = $"{Roles.Admin},{Roles.BankManager}")]
//
// Rollhierarki:
//   Admin       → Full åtkomst till allt
//   BankManager → Kan se allt och blockera kort
//   Teller      → Kan se allt men inte blockera kort
//   Auditor     → Kan bara läsa – inga skrivoperationer
//   User        → Ser bara sina egna konton och transaktioner
// ============================================================

namespace NexaPay.Application.Common.Constants
{
    // static = kan inte instansieras – bara statiska medlemmar
    public static class Roles
    {
        // --------------------------------------------------------
        // Rollnamn som konstanter
        // --------------------------------------------------------

        // Admin – full åtkomst till hela systemet
        // Kan hantera användare, blockera kort och se allt
        public const string Admin = "Admin";

        // BankManager – bankchef
        // Kan se alla konton och transaktioner
        // Kan blockera kort men inte administrera systemet
        public const string BankManager = "BankManager";

        // Teller – bankkassör/kundtjänst
        // Kan se alla konton för att hjälpa kunder
        // Kan INTE blockera kort eller göra adminoperationer
        public const string Teller = "Teller";

        // Auditor – revisor
        // Kan bara LÄSA data – inga skrivoperationer tillåtna
        // Perfekt för compliance och revision
        public const string Auditor = "Auditor";

        // User – vanlig kund
        // Ser bara sina egna konton och transaktioner
        // Kan göra egna transaktioner
        public const string User = "User";

        // --------------------------------------------------------
        // Kombinerade roller för [Authorize]-attribut
        // --------------------------------------------------------
        // Dessa konstanter kombinerar flera roller för endpoints
        // som ska vara tillgängliga för flera roller samtidigt

        // Alla personalroller – kan se alla konton
        // Används för endpoints som bankpersonal ska nå
        public const string AllStaff =
            $"{Admin},{BankManager},{Teller},{Auditor}";

        // Roller som kan blockera kort
        public const string CanBlockCard =
            $"{Admin},{BankManager}";

        // Roller som kan se alla konton (inte bara egna)
        public const string CanViewAllAccounts =
            $"{Admin},{BankManager},{Teller},{Auditor}";

        // Roller som kan utföra skrivoperationer på konton
        public const string CanWriteAccounts =
            $"{Admin},{BankManager},{Teller}";
    }
}