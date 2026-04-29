// ============================================================
// AuthServiceTests.cs – NexaPay.Tests/Infrastructure/Identity
// ============================================================
// Testar AuthService – registrering och inloggning.
//
// AuthService använder ASP.NET Core Identity (UserManager,
// RoleManager) som är svåra att mocka direkt.
// Vi använder Moq för att simulera deras beteende.
//
// Vi testar:
//   1. Lyckad registrering – användare skapas och token returneras
//   2. Registrering misslyckas om e-post redan används
//   3. Registrering misslyckas om Identity returnerar fel
//   4. Lyckad inloggning – token returneras
//   5. Inloggning misslyckas med fel lösenord
//   6. Inloggning misslyckas om användaren inte finns
// ============================================================

using Microsoft.AspNetCore.Identity;
using Moq;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Infrastructure.Identity;
using NUnit.Framework;
using FluentAssertions;

namespace NexaPay.Tests.Infrastructure.Identity
{
    [TestFixture]
    [Category("Infrastructure")]
    [Category("Identity")]
    [Category("Auth")]
    public class AuthServiceTests
    {
        // --------------------------------------------------------
        // Beroenden vi behöver mocka
        // --------------------------------------------------------

        // UserManager hanterar användare i Identity
        // Den är generic och tar IdentityUser som typparameter
        private Mock<UserManager<IdentityUser>> _mockUserManager = null!;

        // RoleManager hanterar roller i Identity
        private Mock<RoleManager<IdentityRole>> _mockRoleManager = null!;

        // IJwtService genererar JWT-tokens
        private Mock<IJwtService> _mockJwtService = null!;

        // AuthService är klassen vi testar
        private AuthService _authService = null!;

        // --------------------------------------------------------
        // Setup – körs innan VARJE test
        // --------------------------------------------------------
        [SetUp]
        public void Setup()
        {
            // ------------------------------------------------
            // Skapa UserManager-mock
            // ------------------------------------------------
            // UserManager har en komplex konstruktor med många
            // beroenden – vi måste skapa en mock av IUserStore
            // och skicka in den till konstruktorn
            var userStoreMock = new Mock<IUserStore<IdentityUser>>();

            _mockUserManager = new Mock<UserManager<IdentityUser>>(
                userStoreMock.Object,
                null!, null!, null!, null!, null!, null!, null!, null!);
            // De null-värdena är valfria beroenden som vi inte behöver

            // ------------------------------------------------
            // Skapa RoleManager-mock
            // ------------------------------------------------
            var roleStoreMock = new Mock<IRoleStore<IdentityRole>>();

            _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
                roleStoreMock.Object,
                null!, null!, null!, null!);

            // ------------------------------------------------
            // Skapa JwtService-mock
            // ------------------------------------------------
            _mockJwtService = new Mock<IJwtService>();

            // Konfigurera JwtService att alltid returnera en token
            // när GenerateToken anropas med vilka parametrar som helst
            _mockJwtService
                .Setup(j => j.GenerateToken(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns("fake-jwt-token-for-testing");

            // ------------------------------------------------
            // Skapa AuthService med våra mocks
            // ------------------------------------------------
            _authService = new AuthService(
                _mockUserManager.Object,
                _mockRoleManager.Object,
                _mockJwtService.Object);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad registrering
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("HappyPath")]
        [Description(
            "Verifierar att en giltig registrering lyckas och " +
            "returnerar en AuthDto med token, email och roll. " +
            "UserManager.CreateAsync ska anropas och " +
            "en JWT-token ska genereras.")]
        public async Task RegisterAsync_WhenValidData_ShouldReturnSuccess()
        {
            // Arrange
            var email = "test@nexapay.com";
            var password = "Test123!";
            var roleName = "User";

            // Konfigurera mocken – e-posten finns inte redan
            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync((IdentityUser?)null);

            // Konfigurera CreateAsync att returnera lyckat resultat
            _mockUserManager
                .Setup(u => u.CreateAsync(
                    It.IsAny<IdentityUser>(),
                    password))
                .ReturnsAsync(IdentityResult.Success);

            // Konfigurera RoleManager – rollen finns redan
            _mockRoleManager
                .Setup(r => r.RoleExistsAsync(roleName))
                .ReturnsAsync(true);

            // Konfigurera AddToRoleAsync att lyckas
            _mockUserManager
                .Setup(u => u.AddToRoleAsync(
                    It.IsAny<IdentityUser>(),
                    roleName))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _authService.RegisterAsync(
                email, password, isAdmin: false);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "en giltig registrering ska lyckas");

            result.Value.Should().NotBeNull(
                "ett lyckat resultat ska innehålla en AuthDto");

            result.Value!.Email.Should().Be(email,
                "AuthDto ska innehålla rätt e-postadress");

            result.Value.Token.Should().Be("fake-jwt-token-for-testing",
                "en JWT-token ska genereras och returneras");

            result.Value.Role.Should().Be(roleName,
                "rollen ska vara User när isAdmin är false");
        }

        // --------------------------------------------------------
        // Test 2: Registrering misslyckas – e-post används redan
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("Validation")]
        [Description(
            "Verifierar att registrering misslyckas när " +
            "e-postadressen redan är registrerad i systemet. " +
            "FindByEmailAsync returnerar en befintlig användare.")]
        public async Task RegisterAsync_WhenEmailAlreadyExists_ShouldReturnFailure()
        {
            // Arrange
            var email = "existing@nexapay.com";

            // Konfigurera mocken – e-posten FINNS redan
            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync(new IdentityUser
                {
                    Email = email,
                    UserName = email
                });

            // Act
            var result = await _authService.RegisterAsync(
                email, "Test123!", isAdmin: false);

            // Assert
            result.IsFailure.Should().BeTrue(
                "registrering ska misslyckas om e-posten redan används");

            result.Error.Should().NotBeEmpty(
                "ett felmeddelande ska finnas");

            // Verifiera att CreateAsync INTE anropades
            // Vi ska inte försöka skapa en användare som redan finns
            _mockUserManager.Verify(
                u => u.CreateAsync(
                    It.IsAny<IdentityUser>(),
                    It.IsAny<string>()),
                Times.Never,
                "CreateAsync ska inte anropas om e-posten redan finns");
        }

        // --------------------------------------------------------
        // Test 3: Registrering misslyckas – Identity returnerar fel
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("ErrorHandling")]
        [Description(
            "Verifierar att registrering misslyckas och returnerar " +
            "felmeddelanden när Identity's CreateAsync misslyckas. " +
            "T.ex. om lösenordet inte uppfyller kraven.")]
        public async Task RegisterAsync_WhenIdentityFails_ShouldReturnFailure()
        {
            // Arrange
            var email = "test@nexapay.com";

            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync((IdentityUser?)null);

            // Konfigurera CreateAsync att MISSLYCKAS
            // IdentityResult.Failed tar emot en lista med fel
            _mockUserManager
                .Setup(u => u.CreateAsync(
                    It.IsAny<IdentityUser>(),
                    It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError
                    {
                        // Simulera ett lösenordsfel från Identity
                        Code = "PasswordTooWeak",
                        Description = "Lösenordet uppfyller inte kraven"
                    }));

            // Act
            var result = await _authService.RegisterAsync(
                email, "weak", isAdmin: false);

            // Assert
            result.IsFailure.Should().BeTrue(
                "registrering ska misslyckas om Identity returnerar fel");

            result.Error.Should().Contain("Lösenordet",
                "felmeddelandet ska innehålla Identitys felbeskrivning");
        }

        // --------------------------------------------------------
        // Test 4: Lyckad inloggning
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("HappyPath")]
        [Description(
            "Verifierar att en lyckad inloggning returnerar " +
            "en AuthDto med token, email och roll. " +
            "Lösenordet kontrolleras mot det hashade värdet.")]
        public async Task LoginAsync_WhenValidCredentials_ShouldReturnSuccess()
        {
            // Arrange
            var email = "test@nexapay.com";
            var password = "Test123!";

            var user = new IdentityUser
            {
                Id = "user-123",
                Email = email,
                UserName = email
            };

            // Konfigurera mocken – användaren finns
            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync(user);

            // Konfigurera lösenordskontrollen att lyckas
            _mockUserManager
                .Setup(u => u.CheckPasswordAsync(user, password))
                .ReturnsAsync(true);

            // Konfigurera GetRolesAsync att returnera User-rollen
            _mockUserManager
                .Setup(u => u.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            // Act
            var result = await _authService.LoginAsync(email, password);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "inloggning ska lyckas med rätt uppgifter");

            result.Value!.Email.Should().Be(email,
                "AuthDto ska innehålla rätt e-postadress");

            result.Value.Token.Should().Be("fake-jwt-token-for-testing",
                "en JWT-token ska genereras");

            result.Value.Role.Should().Be("User",
                "rollen ska matcha vad GetRolesAsync returnerade");

            // Verifiera att GenerateToken anropades med rätt parametrar
            _mockJwtService.Verify(
                j => j.GenerateToken(
                    user.Id,
                    email,
                    "User"),
                Times.Once,
                "GenerateToken ska anropas en gång med rätt parametrar");
        }

        // --------------------------------------------------------
        // Test 5: Inloggning misslyckas – fel lösenord
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("Security")]
        [Description(
            "Verifierar att inloggning misslyckas med ett generiskt " +
            "felmeddelande när fel lösenord anges. " +
            "Vi avslöjar inte om e-posten finns eller inte.")]
        public async Task LoginAsync_WhenWrongPassword_ShouldReturnFailure()
        {
            // Arrange
            var email = "test@nexapay.com";

            var user = new IdentityUser
            {
                Email = email,
                UserName = email
            };

            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync(user);

            // Lösenordskontrollen MISSLYCKAS
            _mockUserManager
                .Setup(u => u.CheckPasswordAsync(
                    user, It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _authService.LoginAsync(
                email, "FelLösenord!");

            // Assert
            result.IsFailure.Should().BeTrue(
                "inloggning ska misslyckas med fel lösenord");

            result.Error.Should().NotBeEmpty(
                "ett felmeddelande ska finnas");

            // Verifiera att GenerateToken INTE anropades
            _mockJwtService.Verify(
                j => j.GenerateToken(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never,
                "ingen token ska genereras vid misslyckad inloggning");
        }

        // --------------------------------------------------------
        // Test 6: Inloggning misslyckas – användaren finns inte
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("NotFound")]
        [Description(
            "Verifierar att inloggning misslyckas med ett generiskt " +
            "felmeddelande när e-postadressen inte finns. " +
            "Samma felmeddelande som fel lösenord – säkerhetsskäl.")]
        public async Task LoginAsync_WhenUserNotFound_ShouldReturnFailure()
        {
            // Arrange
            // Konfigurera mocken – användaren finns INTE
            _mockUserManager
                .Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((IdentityUser?)null);

            // Act
            var result = await _authService.LoginAsync(
                "nonexistent@nexapay.com", "Test123!");

            // Assert
            result.IsFailure.Should().BeTrue(
                "inloggning ska misslyckas om användaren inte finns");

            result.Error.Should().NotBeEmpty(
                "ett felmeddelande ska finnas");

            // Verifiera att CheckPasswordAsync INTE anropades
            // Vi kan inte kontrollera lösenord om användaren inte finns
            _mockUserManager.Verify(
                u => u.CheckPasswordAsync(
                    It.IsAny<IdentityUser>(),
                    It.IsAny<string>()),
                Times.Never,
                "CheckPasswordAsync ska inte anropas om " +
                "användaren inte finns");
        }
    }
}