// ============================================================
// StaffEmailPolicy.cs – NexaPay.Application/Common/Policies
// ============================================================
// Säkerhetsregel: personalroller (Admin, BankManager, Teller,
// Auditor) MÅSTE registreras med en e-post som slutar på
// konfigurerad personal-domän (t.ex. "@nexapay.com"). Detta
// hindrar att vem som helst registrerar sig som personal med
// en privat e-postadress.
//
// Returnerar null när regeln är uppfylld (eller rollen är User),
// annars ett felmeddelande som validators/handlers kan skicka
// vidare till klienten.
// ============================================================

using NexaPay.Application.Common.Constants;
using NexaPay.Application.Common.Interfaces;

namespace NexaPay.Application.Common.Policies
{
    public interface IStaffEmailPolicy
    {
        // Returnerar null när email/roll-kombinationen är giltig,
        // annars ett felmeddelande om personal-domänregeln brutits.
        string? Validate(string email, string role);
    }

    public class StaffEmailPolicy : IStaffEmailPolicy
    {
        private readonly IAppSettings _appSettings;

        public StaffEmailPolicy(IAppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        public string? Validate(string email, string role)
        {
            if (role == Roles.User)
                return null;

            var domain = _appSettings.StaffDomain;
            if (string.IsNullOrWhiteSpace(domain) || !domain.Contains('.'))
                return "StaffDomain är felaktigt konfigurerat – kontakta systemadministratören.";

            if (email.EndsWith($"@{domain}", StringComparison.OrdinalIgnoreCase))
                return null;

            return $"Personalroller kräver en @{domain}-e-postadress";
        }
    }
}
