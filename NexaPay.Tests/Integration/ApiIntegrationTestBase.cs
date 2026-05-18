// ============================================================
// ApiIntegrationTestBase.cs – NexaPay.Tests/Integration
// ============================================================
// Gemensam basklass för integrationstester. Ger:
//   * HttpClient mot in-memory-servern.
//   * Seedade testanvändare (Admin + User) som inloggas via
//     riktiga /api/auth/login-anrop så vi får giltiga JWT.
//   * Hjälpmetoder för att sätta Authorization-headern.
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NexaPay.Application.Common.Constants;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NexaPay.Tests.Integration
{
    public abstract class ApiIntegrationTestBase : IDisposable
    {
        protected readonly NexaPayWebApplicationFactory Factory;
        protected readonly HttpClient Client;

        protected static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        protected ApiIntegrationTestBase()
        {
            Factory = new NexaPayWebApplicationFactory();
            Client = Factory.CreateClient();
        }

        // Registrera, bekräfta e-post via UserManager, logga in – returnera JWT-token
        protected async Task<string> RegisterAndLoginAsync(
            string email,
            string password = "Test123!",
            string role = "User")
        {
            await Client.PostAsJsonAsync("/api/auth/register", new { email, password, role });

            // Bekräfta e-post direkt via UserManager (kringgår SMTP i tester)
            using (var scope = Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider
                    .GetRequiredService<UserManager<IdentityUser>>();
                var user = await userManager.FindByEmailAsync(email);
                if (user != null)
                {
                    var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                    await userManager.ConfirmEmailAsync(user, token);
                }
            }

            var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new { email, password });
            var body = await loginResponse.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            return doc.RootElement
                .GetProperty("data")
                .GetProperty("token")
                .GetString()!;
        }

        // Skapar en Admin-användare direkt i databasen (kringgår API)
        // och returnerar en giltig Admin JWT-token.
        // Används för att testa Admin-skyddade endpoints.
        protected async Task<string> CreateAndLoginAsAdminAsync(
            string? email = null,
            string password = "Admin123!")
        {
            // Unik e-post per anrop – undviker krock med seedad admin@nexapay.com
            email ??= $"admin_{Guid.NewGuid():N}@nexapay.com";

            using var scope = Factory.Services.CreateScope();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<IdentityUser>>();

            var user = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            await userManager.CreateAsync(user, password);
            await userManager.AddToRoleAsync(user, Roles.Admin);

            var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password
            });

            var body = await loginResponse.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            return doc.RootElement
                .GetProperty("data")
                .GetProperty("token")
                .GetString()!;
        }

        protected void SetBearerToken(string token) =>
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

        protected void ClearToken() =>
            Client.DefaultRequestHeaders.Authorization = null;

        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
        }
    }
}
