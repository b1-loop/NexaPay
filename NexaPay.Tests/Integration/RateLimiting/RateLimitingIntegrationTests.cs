using FluentAssertions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace NexaPay.Tests.Integration.RateLimiting
{
    // Ärver NexaPayWebApplicationFactory (InMemory DB + rollseeding) och
    // lägger på en riktig "financial"-gräns (1 req/min) i ett extra
    // ConfigureServices-anrop som stackas efter basklassens konfiguration.
    // "auth" behåller basklassens no-limit så att register/login alltid fungerar.
    internal class RateLimitingWebApplicationFactory : NexaPayWebApplicationFactory
    {
        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            // Kör basklassens setup: InMemory DB + no-limit för auth och financial
            base.ConfigureWebHost(builder);

            // Lägg till ytterligare ett ConfigureServices-anrop som körs sist.
            // Tar bort de no-limit policies som basen lade till och ersätter
            // financial med en riktig gräns på 1 req/min.
            builder.ConfigureServices(services =>
            {
                services
                    .Where(d => d.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>))
                    .ToList()
                    .ForEach(d => services.Remove(d));

                services.Configure<RateLimiterOptions>(options =>
                {
                    options.RejectionStatusCode = 429;
                    options.AddPolicy("auth",
                        _ => RateLimitPartition.GetNoLimiter("test-auth"));
                    options.AddPolicy("financial",
                        _ => RateLimitPartition.GetFixedWindowLimiter(
                            "test-financial",
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 1,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0
                            }));
                });
            });
        }
    }

    [TestFixture]
    [Category("Integration")]
    [Category("RateLimiting")]
    public class RateLimitingIntegrationTests
    {
        // Skapas per test i [SetUp] så att varje test får ett eget
        // server-instance med en färsk rate limiter-bucket.
        private RateLimitingWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;

        [SetUp]
        public void SetUp()
        {
            _factory = new RateLimitingWebApplicationFactory();
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        private async Task<string> RegisterAndLoginAsync(string email)
        {
            await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password = "Test123!",
                role = "User"
            });

            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = "Test123!"
            });

            var body = await loginResponse.Content.ReadAsStringAsync();
            return JsonDocument.Parse(body)
                .RootElement.GetProperty("data").GetProperty("token").GetString()!;
        }

        // --------------------------------------------------------
        // Test 1: Första anropet passerar rate limiten
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att det första anropet till ett finansiellt endpoint " +
            "alltid tillåts igenom (rate limit = 1 req/min).")]
        public async Task FinancialEndpoint_FirstRequest_IsAllowed()
        {
            var token = await RegisterAndLoginAsync($"rl1_{Guid.NewGuid()}@test.com");
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PostAsJsonAsync("/api/accounts", new
            {
                accountName = "Testkonto",
                accountType = 0
            });

            response.StatusCode.Should().Be(HttpStatusCode.Created,
                "det första anropet ska tillåtas av rate limiten");
        }

        // --------------------------------------------------------
        // Test 2: Andra anropet blockeras → 429
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att ett andra anrop till ett finansiellt endpoint " +
            "blockeras med 429 Too Many Requests när rate limit (1 req/min) " +
            "har överskridits.")]
        public async Task FinancialEndpoint_AfterLimitExceeded_Returns429()
        {
            var token = await RegisterAndLoginAsync($"rl2_{Guid.NewGuid()}@test.com");
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            // Första anropet – ska gå igenom
            await _client.PostAsJsonAsync("/api/accounts", new
            {
                accountName = "Testkonto",
                accountType = 0
            });

            // Andra anropet – ska blockeras
            var response = await _client.PostAsJsonAsync("/api/accounts", new
            {
                accountName = "Testkonto 2",
                accountType = 0
            });

            response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
                "andra anropet inom ett fönster ska returnera 429");
        }

        // --------------------------------------------------------
        // Test 3: Auth-endpoints rate-limiteras inte
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att auth-endpoints (register/login) inte rate-limiteras " +
            "och tillåter flera anrop i rad utan att returnera 429.")]
        public async Task AuthEndpoints_AreNotRateLimited_AllowMultipleRequests()
        {
            var statusCodes = new List<HttpStatusCode>();

            for (var i = 0; i < 3; i++)
            {
                var response = await _client.PostAsJsonAsync("/api/auth/register", new
                {
                    email = $"multi_{i}_{Guid.NewGuid()}@test.com",
                    password = "Test123!",
                    role = "User"
                });
                statusCodes.Add(response.StatusCode);
            }

            statusCodes.Should().NotContain(HttpStatusCode.TooManyRequests,
                "auth-endpoints ska aldrig returnera 429 i denna testkonfiguration");
        }

    }
}
