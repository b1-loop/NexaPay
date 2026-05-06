// ============================================================
// LoginValidatorTests.cs
// NexaPay.Tests/Application/Validators
// ============================================================
// Testar LoginValidator.
//
// Vi testar:
//   1. Giltigt command – ska passera
//   2. Tom e-post
//   3. Ogiltig e-postadress
//   4. Tomt lösenord
// ============================================================

using FluentAssertions;
using NexaPay.Application.Features.Auth.Commands.Login;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Validators
{
    [TestFixture]
    [Category("Application")]
    [Category("Validators")]
    [Category("Auth")]
    public class LoginValidatorTests
    {
        private LoginValidator _validator = null!;

        [SetUp]
        public void Setup()
        {
            _validator = new LoginValidator();
        }

        // --------------------------------------------------------
        // Test 1: Giltigt command
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att ett korrekt LoginCommand " +
            "passerar alla valideringsregler.")]
        public async Task Validate_WhenValidCommand_ShouldPass()
        {
            // Arrange
            var command = new LoginCommand
            {
                Email = "anna@nexapay.se",
                Password = "VadSomHelst123!"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue(
                "ett korrekt logincommand ska passera");

            result.Errors.Should().BeEmpty();
        }

        // --------------------------------------------------------
        // Test 2: Tom e-post
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Description(
            "Verifierar att inloggning misslyckas om e-postadressen " +
            "är tom.")]
        public async Task Validate_WhenEmptyEmail_ShouldFail()
        {
            // Arrange
            var command = new LoginCommand
            {
                Email = string.Empty,
                Password = "Secure1!"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "tom e-post ska inte vara giltig");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Email");
        }

        // --------------------------------------------------------
        // Test 3: Ogiltig e-postadress
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Description(
            "Verifierar att en e-post utan korrekt format misslyckas. " +
            "LoginValidator kräver giltig e-postadress.")]
        public async Task Validate_WhenInvalidEmailFormat_ShouldFail()
        {
            // Arrange
            var command = new LoginCommand
            {
                Email = "inteengiltigepost",
                Password = "Secure1!"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "felaktigt e-postformat ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Email");
        }

        // --------------------------------------------------------
        // Test 4: Tomt lösenord
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Description(
            "Verifierar att inloggning misslyckas om lösenordet är tomt. " +
            "LoginValidator validerar bara att lösenordet finns – " +
            "inte komplexiteten (det görs vid registrering).")]
        public async Task Validate_WhenEmptyPassword_ShouldFail()
        {
            // Arrange
            var command = new LoginCommand
            {
                Email = "anna@nexapay.se",
                Password = string.Empty
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "tomt lösenord ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Password");
        }
    }
}
