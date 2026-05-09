// ============================================================
// RegisterHandlerTests.cs
// NexaPay.Tests/Application/Features/Auth
// ============================================================
// Testar RegisterHandler – domänbaserad rollbegränsning.
//
// Vi testar:
//   1. Staff-e-post + personalroll  → tillåtet
//   2. Extern e-post + personalroll → nekat, explicit fel
//   3. Extern e-post + User-roll    → tillåtet
//   4. Staff-e-post + User-roll     → tillåtet
//   5. Admin-roll kräver staff-epost
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.Common.Models;
using NexaPay.Application.Common.Policies;
using NexaPay.Application.DTOs;
using NexaPay.Application.Features.Auth.Commands.Register;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Auth
{
    [TestFixture]
    [Category("Application")]
    [Category("Auth")]
    [Category("Register")]
    public class RegisterHandlerTests
    {
        private Mock<IAuthService> _mockAuthService = null!;
        private RegisterHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            _mockAuthService = new Mock<IAuthService>();

            var mockAppSettings = new Mock<IAppSettings>();
            mockAppSettings.Setup(s => s.StaffDomain).Returns("nexapay.com");

            _mockAuthService
                .Setup(a => a.RegisterAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(Result<AuthDto>.Success(new AuthDto
                {
                    Token = "fake-token",
                    Email = "test@nexapay.com",
                    Role = Roles.Admin,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                }));

            _handler = new RegisterHandler(
                _mockAuthService.Object,
                new StaffEmailPolicy(mockAppSettings.Object));
        }

        // --------------------------------------------------------
        // Test 1: Staff-e-post + personalroll → tillåtet
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "En nexapay.com-e-post ska kunna registrera sig " +
            "med en personalroll som Admin.")]
        public async Task Handle_WhenStaffEmailAndStaffRole_ShouldAllow()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "chef@nexapay.com",
                Password = "Secure1!",
                Role = Roles.Admin
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "nexapay.com-epost ska kunna registrera personalroller");

            _mockAuthService.Verify(
                a => a.RegisterAsync("chef@nexapay.com", "Secure1!", Roles.Admin),
                Times.Once);
        }

        // --------------------------------------------------------
        // Test 2: Extern e-post + personalroll → nekat
        // --------------------------------------------------------
        [Test]
        [Category("Security")]
        [Description(
            "En extern e-post (ej nexapay.com) ska inte kunna " +
            "registrera sig med en personalroll. " +
            "Returnerar ett explicit fel.")]
        public async Task Handle_WhenExternalEmailAndStaffRole_ShouldReturnFailure()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "hacker@gmail.com",
                Password = "Secure1!",
                Role = Roles.Admin
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "extern e-post ska inte kunna registrera personalroller");

            result.Error.Should().Contain("nexapay.com",
                "felmeddelandet ska ange vilken domän som krävs");

            _mockAuthService.Verify(
                a => a.RegisterAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never,
                "AuthService ska inte anropas när domänen inte matchar");
        }

        // --------------------------------------------------------
        // Test 3: Extern e-post + User-roll → tillåtet
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "En vanlig kund med extern e-post ska kunna " +
            "registrera sig med User-rollen.")]
        public async Task Handle_WhenExternalEmailAndUserRole_ShouldAllow()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "kund@gmail.com",
                Password = "Secure1!",
                Role = Roles.User
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "extern e-post ska kunna registrera sig med User-roll");

            _mockAuthService.Verify(
                a => a.RegisterAsync("kund@gmail.com", "Secure1!", Roles.User),
                Times.Once);
        }

        // --------------------------------------------------------
        // Test 4: Staff-e-post + User-roll → tillåtet
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "En nexapay.com-e-post ska också kunna registrera " +
            "sig som vanlig User.")]
        public async Task Handle_WhenStaffEmailAndUserRole_ShouldAllow()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "praktikant@nexapay.com",
                Password = "Secure1!",
                Role = Roles.User
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "nexapay.com-epost ska kunna registrera User-roll");

            _mockAuthService.Verify(
                a => a.RegisterAsync("praktikant@nexapay.com", "Secure1!", Roles.User),
                Times.Once);
        }

        // --------------------------------------------------------
        // Test 5: Alla personalroller kräver staff-epost
        // --------------------------------------------------------
        [TestCase(Roles.Admin)]
        [TestCase(Roles.BankManager)]
        [TestCase(Roles.Teller)]
        [TestCase(Roles.Auditor)]
        [Category("Security")]
        [Description(
            "Alla personalroller (Admin, BankManager, Teller, Auditor) " +
            "ska nekas för externa e-postadresser.")]
        public async Task Handle_WhenExternalEmailAndAnyStaffRole_ShouldReturnFailure(
            string staffRole)
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "extern@hotmail.com",
                Password = "Secure1!",
                Role = staffRole
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                $"rollen '{staffRole}' ska kräva nexapay.com-epost");

            _mockAuthService.Verify(
                a => a.RegisterAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never);
        }
    }
}
