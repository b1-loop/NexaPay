// ============================================================
// AuthServiceTests.cs – NexaPay.Tests/Infrastructure/Identity
// ============================================================
// Testar AuthService – registrering och inloggning.
// Uppdaterad för att använda rollsträng istället för isAdmin.
// ============================================================

using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using NexaPay.Application.Common.Constants;
using NexaPay.Infrastructure.Identity;
using NUnit.Framework;

namespace NexaPay.Tests.Infrastructure.Identity
{
    [TestFixture]
    [Category("Infrastructure")]
    [Category("Identity")]
    [Category("Auth")]
    public class AuthServiceTests
    {
        private Mock<UserManager<IdentityUser>> _mockUserManager = null!;
        private Mock<RoleManager<IdentityRole>> _mockRoleManager = null!;
        private Mock<IJwtService> _mockJwtService = null!;
        private AuthService _authService = null!;

        [SetUp]
        public void Setup()
        {
            // Skapa UserManager-mock
            var userStoreMock = new Mock<IUserStore<IdentityUser>>();
            _mockUserManager = new Mock<UserManager<IdentityUser>>(
                userStoreMock.Object,
                null!, null!, null!, null!, null!, null!, null!, null!);

            // Skapa RoleManager-mock
            var roleStoreMock = new Mock<IRoleStore<IdentityRole>>();
            _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
                roleStoreMock.Object,
                null!, null!, null!, null!);

            // Skapa JwtService-mock
            _mockJwtService = new Mock<IJwtService>();

            // JwtService returnerar alltid en testtoken
            _mockJwtService
                .Setup(j => j.GenerateToken(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(new TokenResult(
                    "fake-jwt-token-for-testing",
                    DateTime.UtcNow.AddHours(24)));

            // Skapa AuthService
            _authService = new AuthService(
                _mockUserManager.Object,
                _mockRoleManager.Object,
                _mockJwtService.Object);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad registrering som User
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("HappyPath")]
        [Description(
            "Verifierar att en giltig registrering med rollen User " +
            "lyckas och returnerar en AuthDto med token och roll. " +
            "CreateAsync ska anropas och token ska genereras.")]
        public async Task RegisterAsync_WhenValidData_ShouldReturnSuccess()
        {
            // Arrange
            var email = "test@nexapay.com";
            var password = "Test123!";
            var role = Roles.User;

            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync((IdentityUser?)null);

            _mockUserManager
                .Setup(u => u.CreateAsync(
                    It.IsAny<IdentityUser>(),
                    password))
                .ReturnsAsync(IdentityResult.Success);

            _mockRoleManager
                .Setup(r => r.RoleExistsAsync(role))
                .ReturnsAsync(true);

            _mockUserManager
                .Setup(u => u.AddToRoleAsync(
                    It.IsAny<IdentityUser>(),
                    role))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _authService.RegisterAsync(
                email, password, role);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "en giltig registrering ska lyckas");

            result.Value!.Email.Should().Be(email,
                "AuthDto ska innehålla rätt e-postadress");

            result.Value.Token.Should().Be("fake-jwt-token-for-testing",
                "en JWT-token ska genereras och returneras");

            result.Value.Role.Should().Be(role,
                "rollen ska matcha det vi angav");
        }

        // --------------------------------------------------------
        // Test 2: Lyckad registrering som Admin
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("HappyPath")]
        [Description(
            "Verifierar att registrering med Admin-rollen fungerar. " +
            "Alla fem roller ska kunna registreras.")]
        public async Task RegisterAsync_WhenAdminRole_ShouldReturnSuccess()
        {
            // Arrange
            var email = "admin@nexapay.com";
            var password = "Admin123!";
            var role = Roles.Admin;

            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync((IdentityUser?)null);

            _mockUserManager
                .Setup(u => u.CreateAsync(
                    It.IsAny<IdentityUser>(),
                    password))
                .ReturnsAsync(IdentityResult.Success);

            _mockRoleManager
                .Setup(r => r.RoleExistsAsync(role))
                .ReturnsAsync(true);

            _mockUserManager
                .Setup(u => u.AddToRoleAsync(
                    It.IsAny<IdentityUser>(),
                    role))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _authService.RegisterAsync(
                email, password, role);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "registrering med Admin-rollen ska lyckas");

            result.Value!.Role.Should().Be(Roles.Admin,
                "rollen ska vara Admin");
        }

        // --------------------------------------------------------
        // Test 3: Registrering misslyckas – ogiltig roll
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("Validation")]
        [Description(
            "Verifierar att registrering misslyckas när " +
            "en ogiltig roll anges. " +
            "T.ex. 'SuperAdmin' är inte en giltig roll i NexaPay.")]
        public async Task RegisterAsync_WhenInvalidRole_ShouldReturnFailure()
        {
            // Arrange
            var email = "test@nexapay.com";

            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync((IdentityUser?)null);

            // Act – skicka en ogiltig roll
            var result = await _authService.RegisterAsync(
                email, "Test123!", "SuperAdmin"); // Ogiltig roll!

            // Assert
            result.IsFailure.Should().BeTrue(
                "registrering ska misslyckas med ogiltig roll");

            result.Error.Should().Contain("Ogiltig roll",
                "felmeddelandet ska nämna att rollen är ogiltig");

            // Verifiera att CreateAsync INTE anropades
            _mockUserManager.Verify(
                u => u.CreateAsync(
                    It.IsAny<IdentityUser>(),
                    It.IsAny<string>()),
                Times.Never,
                "CreateAsync ska inte anropas vid ogiltig roll");
        }

        // --------------------------------------------------------
        // Test 4: Registrering misslyckas – e-post används redan
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("Validation")]
        [Description(
            "Verifierar att registrering misslyckas när " +
            "e-postadressen redan är registrerad i systemet.")]
        public async Task RegisterAsync_WhenEmailAlreadyExists_ShouldReturnFailure()
        {
            // Arrange
            var email = "existing@nexapay.com";

            // E-posten FINNS redan
            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync(new IdentityUser
                {
                    Email = email,
                    UserName = email
                });

            // Act
            var result = await _authService.RegisterAsync(
                email, "Test123!", Roles.User);

            // Assert
            result.IsFailure.Should().BeTrue(
                "registrering ska misslyckas om e-posten redan används");

            _mockUserManager.Verify(
                u => u.CreateAsync(
                    It.IsAny<IdentityUser>(),
                    It.IsAny<string>()),
                Times.Never,
                "CreateAsync ska inte anropas om e-posten redan finns");
        }

        // --------------------------------------------------------
        // Test 5: Registrering misslyckas – Identity returnerar fel
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("ErrorHandling")]
        [Description(
            "Verifierar att Identity-fel propageras korrekt. " +
            "T.ex. om lösenordet inte uppfyller kraven.")]
        public async Task RegisterAsync_WhenIdentityFails_ShouldReturnFailure()
        {
            // Arrange
            var email = "test@nexapay.com";

            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync((IdentityUser?)null);

            // CreateAsync MISSLYCKAS
            _mockUserManager
                .Setup(u => u.CreateAsync(
                    It.IsAny<IdentityUser>(),
                    It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "PasswordTooWeak",
                        Description = "Lösenordet uppfyller inte kraven"
                    }));

            // Act
            var result = await _authService.RegisterAsync(
                email, "weak", Roles.User);

            // Assert
            result.IsFailure.Should().BeTrue(
                "registrering ska misslyckas om Identity returnerar fel");

            result.Error.Should().Contain("Lösenordet",
                "felmeddelandet ska innehålla Identitys felbeskrivning");
        }

        // --------------------------------------------------------
        // Test 6: Lyckad inloggning
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("HappyPath")]
        [Description(
            "Verifierar att en lyckad inloggning returnerar " +
            "en AuthDto med token, email och roll. " +
            "ResetAccessFailedCountAsync ska anropas för att " +
            "nollställa räknaren för misslyckade försök.")]
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

            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync(user);

            // Kontot är INTE låst
            _mockUserManager
                .Setup(u => u.IsLockedOutAsync(user))
                .ReturnsAsync(false);

            _mockUserManager
                .Setup(u => u.CheckPasswordAsync(user, password))
                .ReturnsAsync(true);

            _mockUserManager
                .Setup(u => u.ResetAccessFailedCountAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            _mockUserManager
                .Setup(u => u.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { Roles.BankManager });

            // Act
            var result = await _authService.LoginAsync(email, password);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "inloggning ska lyckas med rätt uppgifter");

            result.Value!.Email.Should().Be(email,
                "AuthDto ska innehålla rätt e-postadress");

            result.Value.Token.Should().Be("fake-jwt-token-for-testing",
                "en JWT-token ska genereras");

            result.Value.Role.Should().Be(Roles.BankManager,
                "rollen ska matcha vad GetRolesAsync returnerade");

            _mockJwtService.Verify(
                j => j.GenerateToken(user.Id, email, Roles.BankManager),
                Times.Once,
                "GenerateToken ska anropas med rätt parametrar");

            _mockUserManager.Verify(
                u => u.ResetAccessFailedCountAsync(user),
                Times.Once,
                "räknaren för misslyckade försök ska nollställas vid lyckad inloggning");
        }

        // --------------------------------------------------------
        // Test 7: Inloggning misslyckas – fel lösenord
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("Security")]
        [Description(
            "Verifierar att inloggning misslyckas med fel lösenord, " +
            "att AccessFailedAsync anropas för att öka lockout-räknaren, " +
            "och att ingen token genereras.")]
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

            // Kontot är INTE låst
            _mockUserManager
                .Setup(u => u.IsLockedOutAsync(user))
                .ReturnsAsync(false);

            // Lösenordskontrollen MISSLYCKAS
            _mockUserManager
                .Setup(u => u.CheckPasswordAsync(
                    user, It.IsAny<string>()))
                .ReturnsAsync(false);

            _mockUserManager
                .Setup(u => u.AccessFailedAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _authService.LoginAsync(
                email, "FelLösenord!");

            // Assert
            result.IsFailure.Should().BeTrue(
                "inloggning ska misslyckas med fel lösenord");

            _mockJwtService.Verify(
                j => j.GenerateToken(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never,
                "ingen token ska genereras vid misslyckad inloggning");

            // Kritisk verifiering: räknaren för misslyckade försök ska ökas
            _mockUserManager.Verify(
                u => u.AccessFailedAsync(user),
                Times.Once,
                "AccessFailedAsync ska anropas för att öka lockout-räknaren");
        }

        // --------------------------------------------------------
        // Test 8: Inloggning misslyckas – användaren finns inte
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("NotFound")]
        [Description(
            "Verifierar att inloggning misslyckas när " +
            "e-postadressen inte finns i systemet.")]
        public async Task LoginAsync_WhenUserNotFound_ShouldReturnFailure()
        {
            // Arrange
            _mockUserManager
                .Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((IdentityUser?)null);

            // Act
            var result = await _authService.LoginAsync(
                "nonexistent@nexapay.com", "Test123!");

            // Assert
            result.IsFailure.Should().BeTrue(
                "inloggning ska misslyckas om användaren inte finns");

            _mockUserManager.Verify(
                u => u.CheckPasswordAsync(
                    It.IsAny<IdentityUser>(),
                    It.IsAny<string>()),
                Times.Never,
                "CheckPasswordAsync ska inte anropas om " +
                "användaren inte finns");
        }

        // --------------------------------------------------------
        // Test 9: Inloggning misslyckas – kontot är låst
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("Security")]
        [Description(
            "Verifierar att inloggning misslyckas direkt när kontot " +
            "är låst, utan att lösenordet kontrolleras eller " +
            "AccessFailedAsync anropas.")]
        public async Task LoginAsync_WhenAccountIsLockedOut_ShouldReturnFailure()
        {
            // Arrange
            var email = "locked@nexapay.com";
            var user = new IdentityUser
            {
                Email = email,
                UserName = email
            };

            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync(user);

            // Kontot ÄR låst
            _mockUserManager
                .Setup(u => u.IsLockedOutAsync(user))
                .ReturnsAsync(true);

            // Act
            var result = await _authService.LoginAsync(email, "Test123!");

            // Assert
            result.IsFailure.Should().BeTrue(
                "inloggning ska misslyckas när kontot är låst");

            result.Error.Should().Contain("låst",
                "felmeddelandet ska informera om att kontot är låst");

            // Lösenordet ska inte kontrolleras när kontot är låst
            _mockUserManager.Verify(
                u => u.CheckPasswordAsync(
                    It.IsAny<IdentityUser>(),
                    It.IsAny<string>()),
                Times.Never,
                "lösenordet ska inte kontrolleras när kontot är låst");

            // AccessFailedAsync ska inte anropas – kontot är redan låst
            _mockUserManager.Verify(
                u => u.AccessFailedAsync(It.IsAny<IdentityUser>()),
                Times.Never,
                "AccessFailedAsync ska inte anropas när kontot redan är låst");
        }

        // --------------------------------------------------------
        // Test 10: ResetAccessFailedCount anropas INTE vid misslyckat lösenord
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("Security")]
        [Description(
            "Verifierar att räknaren för misslyckade försök INTE " +
            "nollställs vid fel lösenord – den ska öka, inte nollställas.")]
        public async Task LoginAsync_WhenWrongPassword_ShouldNotResetFailedCount()
        {
            // Arrange
            var email = "test@nexapay.com";
            var user = new IdentityUser { Email = email, UserName = email };

            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync(user);

            _mockUserManager
                .Setup(u => u.IsLockedOutAsync(user))
                .ReturnsAsync(false);

            _mockUserManager
                .Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>()))
                .ReturnsAsync(false);

            _mockUserManager
                .Setup(u => u.AccessFailedAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            await _authService.LoginAsync(email, "FelLösenord!");

            // Assert
            _mockUserManager.Verify(
                u => u.ResetAccessFailedCountAsync(It.IsAny<IdentityUser>()),
                Times.Never,
                "räknaren ska inte nollställas vid fel lösenord");
        }
    }
}