using NexaPay.Application.Common.Constants;
using NexaPay.Application.Common.Interfaces;

namespace NexaPay.Application.Common.Policies
{
    public interface IStaffEmailPolicy
    {
        // Returns null when the email/role combination is valid,
        // or an error message when the staff-domain constraint is violated.
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

            if (email.EndsWith(
                    $"@{_appSettings.StaffDomain}",
                    StringComparison.OrdinalIgnoreCase))
                return null;

            return $"Personalroller kräver en @{_appSettings.StaffDomain}-e-postadress";
        }
    }
}
