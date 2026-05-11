using FluentAssertions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace NexaPay.Tests.Integration.Auth
{
    [TestFixture]
    [Category("Integration")]
    [Category("Auth")]
    public class AuthIntegrationTests : ApiIntegrationTestBase
    {
        // --------------------------------------------------------
        // Test 1: Registrering returnerar bekräftelsemedelande (ingen token)
        // --------------------------------------------------------
        [Test]
        public async Task Register_WithValidData_Returns200WithConfirmationMessage()
        {
            var response = await Client.PostAsJsonAsync("/api/auth/register", new
            {
                email = $"user_{Guid.NewGuid()}@test.com",
                password = "Test123!",
                role = "User"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("bekräfta", "registrering kräver nu e-postbekräftelse");
        }

        // --------------------------------------------------------
        // Test 2: Registrering med e-post som redan finns → 400
        // --------------------------------------------------------
        [Test]
        public async Task Register_WithDuplicateEmail_Returns400()
        {
            var email = $"dup_{Guid.NewGuid()}@test.com";

            await Client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password = "Test123!",
                role = "User"
            });

            var response = await Client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password = "Test123!",
                role = "User"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // --------------------------------------------------------
        // Test 3: Inloggning med korrekta uppgifter och bekräftad e-post → 200 med token
        // --------------------------------------------------------
        [Test]
        public async Task Login_WithValidCredentials_Returns200WithToken()
        {
            var token = await RegisterAndLoginAsync($"login_{Guid.NewGuid()}@test.com");
            token.Should().NotBeNullOrEmpty("inloggning med bekräftad e-post ska ge JWT-token");
        }

        // --------------------------------------------------------
        // Test 4: Inloggning med fel lösenord → 401
        // --------------------------------------------------------
        [Test]
        public async Task Login_WithWrongPassword_Returns401()
        {
            var email = $"wrong_{Guid.NewGuid()}@test.com";
            await Client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password = "Test123!",
                role = "User"
            });

            var response = await Client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = "FelLösenord!"
            });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // --------------------------------------------------------
        // Test 5: Skyddad endpoint utan token → 401
        // --------------------------------------------------------
        [Test]
        public async Task ProtectedEndpoint_WithoutToken_Returns401()
        {
            ClearToken();
            var response = await Client.GetAsync("/api/accounts");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // --------------------------------------------------------
        // Test 6: Logout återkallar token – efterföljande request → 401
        // --------------------------------------------------------
        [Test]
        public async Task Logout_RevokesToken_SubsequentRequestReturns401()
        {
            var token = await RegisterAndLoginAsync($"logout_{Guid.NewGuid()}@test.com");
            SetBearerToken(token);

            // Verifiera att token fungerar före logout
            var before = await Client.GetAsync("/api/accounts");
            before.StatusCode.Should().Be(HttpStatusCode.OK);

            // Logga ut
            var logoutResponse = await Client.PostAsync("/api/auth/logout", null);
            logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Samma token ska nu ge 401
            var after = await Client.GetAsync("/api/accounts");
            after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // --------------------------------------------------------
        // Test 7: Personalroll avvisas på publik endpoint
        // --------------------------------------------------------
        [Test]
        public async Task Register_StaffRoleOnPublicEndpoint_Returns400()
        {
            var response = await Client.PostAsJsonAsync("/api/auth/register", new
            {
                email = $"user_{Guid.NewGuid()}@nexapay.com",
                password = "Test123!",
                role = "Admin"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // --------------------------------------------------------
        // Test 8: Admin kan skapa personalanvändare
        // --------------------------------------------------------
        [Test]
        public async Task AdminCreateUser_WithValidStaffRole_Returns200()
        {
            var adminToken = await CreateAndLoginAsAdminAsync();
            SetBearerToken(adminToken);

            var response = await Client.PostAsJsonAsync("/api/admin/users", new
            {
                email = $"teller_{Guid.NewGuid()}@nexapay.com",
                password = "Staff123!",
                role = "Teller"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // --------------------------------------------------------
        // Test 9: Admin kräver @nexapay.com för personalroller
        // --------------------------------------------------------
        [Test]
        public async Task AdminCreateUser_StaffRoleWithExternalEmail_Returns400()
        {
            var adminToken = await CreateAndLoginAsAdminAsync();
            SetBearerToken(adminToken);

            var response = await Client.PostAsJsonAsync("/api/admin/users", new
            {
                email = $"teller_{Guid.NewGuid()}@gmail.com",
                password = "Staff123!",
                role = "Teller"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // --------------------------------------------------------
        // Test 10: Icke-admin kan inte använda admin-endpoint
        // --------------------------------------------------------
        [Test]
        public async Task AdminCreateUser_WithoutAdminRole_Returns403()
        {
            var userToken = await RegisterAndLoginAsync($"user_{Guid.NewGuid()}@test.com");
            SetBearerToken(userToken);

            var response = await Client.PostAsJsonAsync("/api/admin/users", new
            {
                email = $"teller_{Guid.NewGuid()}@nexapay.com",
                password = "Staff123!",
                role = "Teller"
            });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
