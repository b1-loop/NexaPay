// ============================================================
// LoginHandlerTests.cs
// NexaPay.Tests/Application/Features/Auth
// ============================================================
// Testar LoginHandler. Handlern är väldigt tunn – den
// vidarebefordrar bara anropet till IAuthService.
//
// Vi testar:
//   1. Lyckad login → AuthDto returneras
//   2. Misslyckad login → Failure-result
//   3. Email och password skickas oförändrade vidare
//   4. Sensitive request – Password syns inte i ToString()
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Application.Features.Auth.Commands.Login;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Auth
{
    [TestFixture]
    [Category("Application")]
    [Category("Auth")]
    [Category("Login")]
    public class LoginHandlerTests
    {
        private Mock<IAuthService> _authService = null!;
        private LoginHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            _authService = new Mock<IAuthService>();
            _handler = new LoginHandler(_authService.Object);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Lyckad login ska returnera AuthDto med token och roll.")]
        public async Task Handle_WhenCredentialsValid_ShouldReturnAuthDto()
        {
            var dto = new AuthDto
            {
                Token = "jwt-token",
                Email = "user@example.com",
                Role = "User",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            _authService
                .Setup(s => s.LoginAsync("user@example.com", "Password1!"))
                .ReturnsAsync(Result<AuthDto>.Success(dto));

            var command = new LoginCommand
            {
                Email = "user@example.com",
                Password = "Password1!"
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.Token.Should().Be("jwt-token");
            result.Value.Role.Should().Be("User");
        }

        [Test]
        [Category("Security")]
        [Description("Fel lösenord ska returnera Failure utan AuthDto.")]
        public async Task Handle_WhenInvalidCredentials_ShouldReturnFailure()
        {
            _authService
                .Setup(s => s.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Result<AuthDto>.Failure("Felaktig e-post eller lösenord"));

            var command = new LoginCommand
            {
                Email = "user@example.com",
                Password = "fel"
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Value.Should().BeNull();
            result.Error.Should().Contain("Felaktig");
        }

        [Test]
        [Category("HappyPath")]
        [Description("Handlern ska skicka email och password oförändrade till IAuthService.")]
        public async Task Handle_ShouldForwardCredentialsToAuthService()
        {
            _authService
                .Setup(s => s.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Result<AuthDto>.Success(new AuthDto()));

            var command = new LoginCommand
            {
                Email = "test@nexapay.com",
                Password = "SuperSecret1!"
            };

            await _handler.Handle(command, CancellationToken.None);

            _authService.Verify(
                s => s.LoginAsync("test@nexapay.com", "SuperSecret1!"),
                Times.Once,
                "credentials ska skickas vidare oförändrade");
        }

        [Test]
        [Category("Security")]
        [Description("LoginCommand.ToString() ska maska lösenordet (skydd mot loggläckage).")]
        public void LoginCommand_ToString_ShouldNotExposePassword()
        {
            var command = new LoginCommand
            {
                Email = "user@example.com",
                Password = "VerySensitive123!"
            };

            var text = command.ToString();

            text.Should().NotContain("VerySensitive123!",
                "lösenordet får inte synas i ToString – LoggingBehavior loggar requesten");
            text.Should().Contain("SKYDDAD", "platshållare ska användas istället");
            text.Should().Contain("user@example.com", "e-post är OK att logga");
        }
    }
}
