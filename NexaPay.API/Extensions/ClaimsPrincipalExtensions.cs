// ============================================================
// ClaimsPrincipalExtensions.cs – NexaPay.API/Extensions
// ============================================================
// Förkortar User.FindFirstValue(ClaimTypes.NameIdentifier) till
// User.GetUserId() i controllers. Encapsulerar också test för
// staff-roller på ETT ställe så att controllers kan skriva
// 'User.IsStaff()' istället för att räkna upp varje roll.
// ============================================================

using NexaPay.Application.Common.Constants;
using System.Security.Claims;

namespace NexaPay.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetUserId(this ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        // "email"-claim sätts direkt av JwtService via JwtRegisteredClaimNames.Email
        public static string GetEmail(this ClaimsPrincipal user) =>
            user.FindFirstValue("email") ?? string.Empty;

        public static string GetRole(this ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        public static bool IsStaff(this ClaimsPrincipal user) =>
            user.IsInRole(Roles.Admin) ||
            user.IsInRole(Roles.BankManager) ||
            user.IsInRole(Roles.Teller) ||
            user.IsInRole(Roles.Auditor);

        public static bool IsAdmin(this ClaimsPrincipal user) =>
            user.IsInRole(Roles.Admin);
    }
}
