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

        // Registrera och logga in – returnera JWT-token
        protected async Task<string> RegisterAndLoginAsync(
            string email,
            string password = "Test123!",
            string role = "User")
        {
            await Client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password,
                role
            });

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
