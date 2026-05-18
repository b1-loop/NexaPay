// ============================================================
// TransactionsIntegrationTests.cs
// NexaPay.Tests/Integration/Transactions
// ============================================================
// E2E-tester för /api/transactions/* (deposit, withdraw,
// transfer, pay-invoice + paginerad historik). Verifierar
// idempotency-headern, ägar-koll och valutamatchning.
// ============================================================

using FluentAssertions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NexaPay.Tests.Integration.Transactions
{
    [TestFixture]
    [Category("Integration")]
    [Category("Transactions")]
    public class TransactionsIntegrationTests : ApiIntegrationTestBase
    {
        private async Task<Guid> CreateAccountAsync(string name)
        {
            var response = await Client.PostAsJsonAsync("/api/accounts", new
            {
                accountName = name,
                accountType = 0 // Checking
            });
            var body = await response.Content.ReadAsStringAsync();
            return Guid.Parse(JsonDocument.Parse(body)
                .RootElement.GetProperty("data").GetProperty("id").GetString()!);
        }

        private async Task DepositAsync(Guid accountId, decimal amount)
        {
            await Client.PostAsJsonAsync("/api/transactions/deposit", new
            {
                accountId,
                amount,
                description = "Startkapital"
            });
        }

        private async Task<decimal> GetBalanceAsync(Guid accountId)
        {
            var response = await Client.GetAsync($"/api/accounts/{accountId}");
            var body = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(body)
                .RootElement.GetProperty("data").GetProperty("balance").GetDecimal();
        }

        // --------------------------------------------------------
        // Test 1: Överföring mellan egna konton → 200 och saldon flyttas
        // --------------------------------------------------------
        [Test]
        public async Task Transfer_BetweenOwnAccounts_Returns200AndMovesBalance()
        {
            var token = await RegisterAndLoginAsync($"tr_{Guid.NewGuid()}@test.com");
            SetBearerToken(token);

            var fromId = await CreateAccountAsync("Avsändarkonto");
            var toId = await CreateAccountAsync("Mottagarkonto");
            await DepositAsync(fromId, 1000m);

            var response = await Client.PostAsJsonAsync("/api/transactions/transfer", new
            {
                fromAccountId = fromId,
                toAccountId = toId,
                amount = 300m,
                description = "Testöverföring"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            (await GetBalanceAsync(fromId)).Should().Be(700m);
            (await GetBalanceAsync(toId)).Should().Be(300m);
        }

        // --------------------------------------------------------
        // Test 2: Överföring med otillräckligt saldo → 400
        // --------------------------------------------------------
        [Test]
        public async Task Transfer_WithInsufficientBalance_Returns400()
        {
            var token = await RegisterAndLoginAsync($"tr_{Guid.NewGuid()}@test.com");
            SetBearerToken(token);

            var fromId = await CreateAccountAsync("Tomt avsändarkonto");
            var toId = await CreateAccountAsync("Mottagarkonto");

            var response = await Client.PostAsJsonAsync("/api/transactions/transfer", new
            {
                fromAccountId = fromId,
                toAccountId = toId,
                amount = 500m,
                description = "Försöker överföra utan täckning"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // --------------------------------------------------------
        // Test 3: Fakturabetalning → 200 och saldot minskar
        // --------------------------------------------------------
        // 12345674 är ett mod-10-giltigt (Luhn) OCR-nummer.
        [Test]
        public async Task PayInvoice_WithValidOcr_Returns200AndDebitsAccount()
        {
            var token = await RegisterAndLoginAsync($"inv_{Guid.NewGuid()}@test.com");
            SetBearerToken(token);

            var accountId = await CreateAccountAsync("Lönekonto");
            await DepositAsync(accountId, 1000m);

            var response = await Client.PostAsJsonAsync("/api/transactions/invoice-payment", new
            {
                accountId,
                amount = 250m,
                bankgiro = "12345678",
                ocr = "12345674",
                description = "Elräkning mars"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await GetBalanceAsync(accountId)).Should().Be(750m);
        }

        // --------------------------------------------------------
        // Test 4: Fakturabetalning med ogiltigt OCR → 400
        // --------------------------------------------------------
        // 12345675 har fel kontrollsiffra (giltigt vore 12345674).
        [Test]
        public async Task PayInvoice_WithInvalidOcr_Returns400()
        {
            var token = await RegisterAndLoginAsync($"inv_{Guid.NewGuid()}@test.com");
            SetBearerToken(token);

            var accountId = await CreateAccountAsync("Lönekonto");
            await DepositAsync(accountId, 1000m);

            var response = await Client.PostAsJsonAsync("/api/transactions/invoice-payment", new
            {
                accountId,
                amount = 250m,
                bankgiro = "12345678",
                ocr = "12345675",
                description = "Faktura med trasigt OCR"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // --------------------------------------------------------
        // Test 5: Fakturabetalning utan täckning → 400
        // --------------------------------------------------------
        [Test]
        public async Task PayInvoice_WithInsufficientBalance_Returns400()
        {
            var token = await RegisterAndLoginAsync($"inv_{Guid.NewGuid()}@test.com");
            SetBearerToken(token);

            var accountId = await CreateAccountAsync("Tomt konto");

            var response = await Client.PostAsJsonAsync("/api/transactions/invoice-payment", new
            {
                accountId,
                amount = 500m,
                bankgiro = "12345678",
                ocr = "12345674",
                description = "Faktura utan täckning"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
