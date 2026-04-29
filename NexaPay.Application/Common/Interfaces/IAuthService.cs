// ============================================================
// IAuthService.cs – NexaPay.Application/Common/Interfaces
// ============================================================
// Kontraktet för autentiseringstjänsten.
// Application-lagret känner bara till detta interface –
// aldrig den konkreta implementationen i Infrastructure.
//
// Genom att använda ett interface kan vi:
//   1. Testa Application-lagret utan en riktig databas
//   2. Byta ut Identity mot något annat utan att röra Application
//   3. Hålla Clean Architecture intakt
// ============================================================

using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Application.Common.Interfaces
{
    public interface IAuthService
    {
        // Registrera en ny användare
        // Returnerar Result med AuthDto om det lyckas
        // AuthDto innehåller token, email och roll
        Task<Result<AuthDto>> RegisterAsync(
            string email,
            string password,
            bool isAdmin);

        // Logga in en befintlig användare
        // Returnerar Result med AuthDto om det lyckas
        Task<Result<AuthDto>> LoginAsync(
            string email,
            string password);
    }
}