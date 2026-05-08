using NexaPay.Application.Common.Constants;
using System.Security.Claims;

namespace NexaPay.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetUserId(this ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        public static bool IsStaff(this ClaimsPrincipal user) =>
            user.IsInRole(Roles.Admin) ||
            user.IsInRole(Roles.BankManager) ||
            user.IsInRole(Roles.Teller) ||
            user.IsInRole(Roles.Auditor);

        public static bool IsAdmin(this ClaimsPrincipal user) =>
            user.IsInRole(Roles.Admin);
    }
}
