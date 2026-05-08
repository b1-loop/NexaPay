using FluentAssertions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NexaPay.Tests.Integration.Accounts
{
    [TestFixture]
    [Category("Integration")]
    [Category("Accounts")]
    public class AccountsIntegrationTests : ApiIntegrationTestBase
    {
        // --------------------------------------------------------
        // Test 1: Skapa konto → 201 Created
        // --------------------------------------------------------
        [Test]
        public async Task CreateAccount_WithValidToken_Returns201()
        {
            var token = await RegisterAndLoginAsync($"acc_{Guid.NewGuid()}@test.com");
            SetBearerToken(token);

            var response = await Client.PostAsJsonAsync("/api/accounts", new
            {
                accountName = "Mitt sparkonto",
                accountType = 1 // Savings
            });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // --------------------------------------------------------
        // Test 2: Hämta konton – User ser bara sina egna
        // --------------------------------------------------------
        [Test]
        public async Task GetAccounts_AsUser_ReturnsOnlyOwnAccounts()
        {
            // Skapa två användare med varsitt konto
            var token1 = await RegisterAndLoginAsync($"u1_{Guid.NewGuid()}@test.com");
            SetBearerToken(token1);
            await Client.PostAsJsonAsync("/api/accounts", new
            {
                accountName = "Konto A",
                accountType = 0
            });

            var token2 = await RegisterAndLoginAsync($"u2_{Guid.NewGuid()}@test.com");
            SetBearerToken(token2);
            await Client.PostAsJsonAsync("/api/accounts", new
            {
                accountName = "Konto B",
                accountType = 0
            });

            // Användare 2 ska bara se sitt eget konto
            var response = await Client.GetAsync("/api/accounts");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            var accounts = doc.RootElement.GetProperty("data");

            accounts.GetArrayLength().Should().Be(1,
                "en User ska bara se sina egna konton");
        }

        // --------------------------------------------------------
        // Test 3: Insättning på eget konto → 200 med transaktionsdata
        // --------------------------------------------------------
        [Test]
        public async Task Deposit_OnOwnAccount_Returns200()
        {
            var token = await RegisterAndLoginAsync($"dep_{Guid.NewGuid()}@test.com");
            SetBearerToken(token);

            var createResponse = await Client.PostAsJsonAsync("/api/accounts", new
            {
                accountName = "Lönekonto",
                accountType = 0
            });

            var createBody = await createResponse.Content.ReadAsStringAsync();
            var accountId = JsonDocument.Parse(createBody)
                .RootElement.GetProperty("data").GetProperty("id").GetString();

            var response = await Client.PostAsJsonAsync("/api/transactions/deposit", new
            {
                accountId = Guid.Parse(accountId!),
                amount = 500m,
                description = "Testinsättning"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("balanceAfterTransaction");
        }

        // --------------------------------------------------------
        // Test 4: Uttag mer än saldo → 400
        // --------------------------------------------------------
        [Test]
        public async Task Withdraw_MoreThanBalance_Returns400()
        {
            var token = await RegisterAndLoginAsync($"wd_{Guid.NewGuid()}@test.com");
            SetBearerToken(token);

            var createResponse = await Client.PostAsJsonAsync("/api/accounts", new
            {
                accountName = "Tomt konto",
                accountType = 0
            });

            var createBody = await createResponse.Content.ReadAsStringAsync();
            var accountId = JsonDocument.Parse(createBody)
                .RootElement.GetProperty("data").GetProperty("id").GetString();

            var response = await Client.PostAsJsonAsync("/api/transactions/withdraw", new
            {
                accountId = Guid.Parse(accountId!),
                amount = 9999m,
                description = "Försöker ta ut för mycket"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // --------------------------------------------------------
        // Test 5: Skapa konto utan token → 401
        // --------------------------------------------------------
        [Test]
        public async Task CreateAccount_WithoutToken_Returns401()
        {
            ClearToken();
            var response = await Client.PostAsJsonAsync("/api/accounts", new
            {
                accountName = "Obehörigt konto",
                accountType = 0
            });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
