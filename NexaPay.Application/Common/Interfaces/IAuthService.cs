// ============================================================
// IAuthService.cs – NexaPay.Application/Common/Interfaces
// ============================================================
// Kontraktet för autentiseringstjänsten.
// RegisterAsync tar nu en roll-sträng istället för bool isAdmin.
// ============================================================

using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Common.Interfaces
{
    public interface IAuthService
    {
        // Registrera en ny användare med en specifik roll
        // role = "Admin", "BankManager", "Teller", "Auditor" eller "User"
        Task<Result<AuthDto>> RegisterAsync(
            string email,
            string password,
            string role);

        // Logga in en befintlig användare
        Task<Result<AuthDto>> LoginAsync(
            string email,
            string password);
    }
}