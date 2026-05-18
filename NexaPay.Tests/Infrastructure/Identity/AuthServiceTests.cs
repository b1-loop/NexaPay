// ============================================================
// AuthServiceTests.cs – NexaPay.Tests/Infrastructure/Identity
// ============================================================
// Enhetstester för AuthService med mockade UserManager,
// RoleManager och JwtService. Täcker registrering, inloggning
// och felscenarier (felaktigt lösenord, obekräftad e-post,
// låst konto).
// ============================================================

using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.Common.Interfaces;
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
        private Mock<INotificationService> _mockNotificationService = null!;
        private AuthService _authService = null!;

        [SetUp]
        public void Setup()
        {
            var userStoreMock = new Mock<IUserStore<IdentityUser>>();
            _mockUserManager = new Mock<UserManager<IdentityUser>>(
                userStoreMock.Object,
                null!, null!, null!, null!, null!, null!, null!, null!);

            var roleStoreMock = new Mock<IRoleStore<IdentityRole>>();
            _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
                roleStoreMock.Object,
                null!, null!, null!, null!);

            _mockJwtService = new Mock<IJwtService>();
            _mockJwtService
                .Setup(j => j.GenerateToken(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new TokenResult(
                    "fake-jwt-token-for-testing",
                    DateTime.UtcNow.AddHours(24)));

            _mockNotificationService = new Mock<INotificationService>();
            _mockNotificationService
                .Setup(n => n.NotifyEmailConfirmationAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockNotificationService
                .Setup(n => n.NotifyPasswordResetAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _authService = new AuthService(
                _mockUserManager.Object,
                _mockRoleManager.Object,
                _mockJwtService.Object,
                _mockNotificationService.Object,
                new Mock<ILogger<AuthService>>().Object);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad registrering – returnerar RequiresEmailConfirmation
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("HappyPath")]
        [Description(
            "Verifierar att en giltig registrering lyckas och signalerar " +
            "att e-postbekräftelse krävs. Ingen JWT-token ska returneras.")]
        public async Task RegisterAsync_WhenValidData_ShouldReturnRequiresEmailConfirmation()
        {
            // Arrange
            var email = "test@nexapay.com";
            var password = "Test123!";
            var role = Roles.User;

            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync((IdentityUser?)null);

            _mockUserManager
                .Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), password))
                .ReturnsAsync(IdentityResult.Success);

            _mockRoleManager
                .Setup(r => r.RoleExistsAsync(role))
                .ReturnsAsync(true);

            _mockUserManager
                .Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), role))
                .ReturnsAsync(IdentityResult.Success);

            _mockUserManager
                .Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<IdentityUser>()))
                .ReturnsAsync("confirm-token-abc");

            // Act
            var result = await _authService.RegisterAsync(email, password, role);

            // Assert
            result.IsSuccess.Should().BeTrue("en giltig registrering ska lyckas");
            result.Value!.Email.Should().Be(email);
            result.Value.RequiresEmailConfirmation.Should().BeTrue(
                "token ska inte utfärdas förrän e-posten bekräftats");
            result.Value.Token.Should().BeEmpty(
                "ingen JWT ska returneras vid obekräftad e-post");

            _mockNotificationService.Verify(
                n => n.NotifyEmailConfirmationAsync(email, "confirm-token-abc", It.IsAny<CancellationToken>()),
                Times.Once,
                "bekräftelsemail ska skickas vid registrering");
        }

        // --------------------------------------------------------
        // Test 2: Registrering med Admin-roll lyckas
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("HappyPath")]
        public async Task RegisterAsync_WhenAdminRole_ShouldReturnSuccess()
        {
            var email = "admin@nexapay.com";
            var password = "Admin123!";
            var role = Roles.Admin;

            _mockUserManager.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync((IdentityUser?)null);
            _mockUserManager.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), password)).ReturnsAsync(IdentityResult.Success);
            _mockRoleManager.Setup(r => r.RoleExistsAsync(role)).ReturnsAsync(true);
            _mockUserManager.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), role)).ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<IdentityUser>())).ReturnsAsync("token");

            var result = await _authService.RegisterAsync(email, password, role);

            result.IsSuccess.Should().BeTrue("registrering med Admin-rollen ska lyckas");
            result.Value!.Role.Should().Be(Roles.Admin);
        }

        // --------------------------------------------------------
        // Test 3: Registrering misslyckas – ogiltig roll
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("Validation")]
        public async Task RegisterAsync_WhenInvalidRole_ShouldReturnFailure()
        {
            var email = "test@nexapay.com";
            _mockUserManager.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync((IdentityUser?)null);

            var result = await _authService.RegisterAsync(email, "Test123!", "SuperAdmin");

            result.IsFailure.Should().BeTrue("ogiltig roll ska ge fel");
            result.Error.Should().Contain("Ogiltig roll");
            _mockUserManager.Verify(u => u.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
        }

        // --------------------------------------------------------
        // Test 4: Registrering misslyckas – e-post används redan
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("Validation")]
        public async Task RegisterAsync_WhenEmailAlreadyExists_ShouldReturnFailure()
        {
            var email = "existing@nexapay.com";
            _mockUserManager
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync(new IdentityUser { Email = email, UserName = email });

            var result = await _authService.RegisterAsync(email, "Test123!", Roles.User);

            result.IsFailure.Should().BeTrue();
            _mockUserManager.Verify(u => u.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
        }

        // --------------------------------------------------------
        // Test 5: Registrering misslyckas – Identity returnerar fel
        // --------------------------------------------------------
        [Test]
        [Category("Register")]
        [Category("ErrorHandling")]
        public async Task RegisterAsync_WhenIdentityFails_ShouldReturnFailure()
        {
            var email = "test@nexapay.com";
            _mockUserManager.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync((IdentityUser?)null);
            _mockUserManager
                .Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError
                {
                    Code = "PasswordTooWeak",
                    Description = "Lösenordet uppfyller inte kraven"
                }));

            var result = await _authService.RegisterAsync(email, "weak", Roles.User);

            result.IsFailure.Should().BeTrue();
            result.Error.Should().Contain("Lösenordet");
        }

        // --------------------------------------------------------
        // Test 6: Lyckad inloggning – bekräftad e-post
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("HappyPath")]
        public async Task LoginAsync_WhenValidCredentials_ShouldReturnSuccess()
        {
            var email = "test@nexapay.com";
            var password = "Test123!";
            var user = new IdentityUser
            {
                Id = "user-123",
                Email = email,
                UserName = email,
                EmailConfirmed = true
            };

            _mockUserManager.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(user);
            _mockUserManager.Setup(u => u.IsLockedOutAsync(user)).ReturnsAsync(false);
            _mockUserManager.Setup(u => u.CheckPasswordAsync(user, password)).ReturnsAsync(true);
            _mockUserManager.Setup(u => u.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { Roles.BankManager });

            var result = await _authService.LoginAsync(email, password);

            result.IsSuccess.Should().BeTrue("inloggning ska lyckas med rätt uppgifter");
            result.Value!.Email.Should().Be(email);
            result.Value.Token.Should().Be("fake-jwt-token-for-testing");
            result.Value.Role.Should().Be(Roles.BankManager);

            _mockJwtService.Verify(j => j.GenerateToken(user.Id, email, Roles.BankManager), Times.Once);
            _mockUserManager.Verify(u => u.ResetAccessFailedCountAsync(user), Times.Once);
        }

        // --------------------------------------------------------
        // Test 7: Inloggning misslyckas – obekräftad e-post
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("Security")]
        [Description("Inloggning ska blockeras tills e-postadressen är bekräftad.")]
        public async Task LoginAsync_WhenEmailNotConfirmed_ShouldReturnFailure()
        {
            var email = "unconfirmed@nexapay.com";
            var user = new IdentityUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = false
            };

            _mockUserManager.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(user);
            _mockUserManager.Setup(u => u.IsLockedOutAsync(user)).ReturnsAsync(false);
            _mockUserManager.Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(true);

            var result = await _authService.LoginAsync(email, "Test123!");

            result.IsFailure.Should().BeTrue("obekräftad e-post ska blockera inloggning");
            // Felmeddelandet ska vara generiskt – får inte avslöja att kontot finns
            // men är obekräftat (account enumeration).
            result.Error.Should().NotContain("bekräftad");
            _mockJwtService.Verify(j => j.GenerateToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // --------------------------------------------------------
        // Test 8: Inloggning misslyckas – fel lösenord
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("Security")]
        public async Task LoginAsync_WhenWrongPassword_ShouldReturnFailure()
        {
            var email = "test@nexapay.com";
            var user = new IdentityUser { Email = email, UserName = email };

            _mockUserManager.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(user);
            _mockUserManager.Setup(u => u.IsLockedOutAsync(user)).ReturnsAsync(false);
            _mockUserManager.Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(false);
            _mockUserManager.Setup(u => u.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);

            var result = await _authService.LoginAsync(email, "FelLösenord!");

            result.IsFailure.Should().BeTrue();
            _mockJwtService.Verify(j => j.GenerateToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockUserManager.Verify(u => u.AccessFailedAsync(user), Times.Once,
                "AccessFailedAsync ska anropas för att öka lockout-räknaren");
        }

        // --------------------------------------------------------
        // Test 9: Inloggning misslyckas – användaren finns inte
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("NotFound")]
        public async Task LoginAsync_WhenUserNotFound_ShouldReturnFailure()
        {
            _mockUserManager
                .Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((IdentityUser?)null);

            var result = await _authService.LoginAsync("nonexistent@nexapay.com", "Test123!");

            result.IsFailure.Should().BeTrue();
            _mockUserManager.Verify(u => u.CheckPasswordAsync(
                It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
        }

        // --------------------------------------------------------
        // Test 10: Inloggning misslyckas – kontot är låst
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("Security")]
        public async Task LoginAsync_WhenAccountIsLockedOut_ShouldReturnFailure()
        {
            var email = "locked@nexapay.com";
            var user = new IdentityUser { Email = email, UserName = email };

            _mockUserManager.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(user);
            _mockUserManager.Setup(u => u.IsLockedOutAsync(user)).ReturnsAsync(true);

            var result = await _authService.LoginAsync(email, "Test123!");

            result.IsFailure.Should().BeTrue();
            // Felmeddelandet ska vara generiskt – får inte avslöja att kontot finns
            // men är låst (account enumeration).
            result.Error.Should().NotContain("låst");
            _mockUserManager.Verify(u => u.CheckPasswordAsync(
                It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
            _mockUserManager.Verify(u => u.AccessFailedAsync(It.IsAny<IdentityUser>()), Times.Never);
        }

        // --------------------------------------------------------
        // Test 11: ResetAccessFailedCount anropas INTE vid fel lösenord
        // --------------------------------------------------------
        [Test]
        [Category("Login")]
        [Category("Security")]
        public async Task LoginAsync_WhenWrongPassword_ShouldNotResetFailedCount()
        {
            var email = "test@nexapay.com";
            var user = new IdentityUser { Email = email, UserName = email };

            _mockUserManager.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(user);
            _mockUserManager.Setup(u => u.IsLockedOutAsync(user)).ReturnsAsync(false);
            _mockUserManager.Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(false);
            _mockUserManager.Setup(u => u.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);

            await _authService.LoginAsync(email, "FelLösenord!");

            _mockUserManager.Verify(u => u.ResetAccessFailedCountAsync(It.IsAny<IdentityUser>()), Times.Never,
                "räknaren ska inte nollställas vid fel lösenord");
        }
    }
}
