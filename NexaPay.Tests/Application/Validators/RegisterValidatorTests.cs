// ============================================================
// RegisterValidatorTests.cs
// NexaPay.Tests/Application/Validators
// ============================================================
// Testar RegisterValidator.
//
// Vi testar:
//   1. Giltigt command – ska passera
//   2. Tom e-post
//   3. Ogiltig e-postadress
//   4. För långt lösenord (< 8 tecken)
//   5. Lösenord utan stor bokstav
//   6. Lösenord utan liten bokstav
//   7. Lösenord utan siffra
//   8. Lösenord utan specialtecken
//   9. Ogiltig roll
//  10. Alla giltiga roller accepteras
// ============================================================

using FluentAssertions;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.Features.Auth.Commands.Register;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Validators
{
    [TestFixture]
    [Category("Application")]
    [Category("Validators")]
    [Category("Auth")]
    public class RegisterValidatorTests
    {
        private RegisterValidator _validator = null!;

        [SetUp]
        public void Setup()
        {
            _validator = new RegisterValidator();
        }

        // --------------------------------------------------------
        // Test 1: Giltigt command
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att ett korrekt RegisterCommand " +
            "passerar alla valideringsregler.")]
        public async Task Validate_WhenValidCommand_ShouldPass()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "anna@nexapay.se",
                Password = "Secure1!",
                Role = Roles.User
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue(
                "ett korrekt registreringscommand ska passera");

            result.Errors.Should().BeEmpty();
        }

        // --------------------------------------------------------
        // Test 2: Tom e-post
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Description(
            "Verifierar att en tom e-postadress misslyckas validering.")]
        public async Task Validate_WhenEmptyEmail_ShouldFail()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = string.Empty,
                Password = "Secure1!",
                Role = Roles.User
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
            "Verifierar att en e-post utan @-tecken misslyckas. " +
            "EmailAddress()-regeln kräver korrekt format.")]
        public async Task Validate_WhenInvalidEmailFormat_ShouldFail()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "inte-en-epost",
                Password = "Secure1!",
                Role = Roles.User
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
        // Test 4: Lösenord för kort
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Password")]
        [Description(
            "Verifierar att ett lösenord kortare än 8 tecken " +
            "misslyckas. MinimumLength(8) kräver minst 8 tecken.")]
        public async Task Validate_WhenPasswordTooShort_ShouldFail()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "test@nexapay.se",
                Password = "Ab1!", // Bara 4 tecken
                Role = Roles.User
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett lösenord kortare än 8 tecken ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Password");
        }

        // --------------------------------------------------------
        // Test 5: Lösenord utan stor bokstav
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Password")]
        [Description(
            "Verifierar att ett lösenord utan stor bokstav misslyckas.")]
        public async Task Validate_WhenPasswordMissingUppercase_ShouldFail()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "test@nexapay.se",
                Password = "secure1!", // Inga versaler
                Role = Roles.User
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "lösenord utan stor bokstav ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Password" &&
                e.ErrorMessage.Contains("stor bokstav"));
        }

        // --------------------------------------------------------
        // Test 6: Lösenord utan liten bokstav
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Password")]
        [Description(
            "Verifierar att ett lösenord utan liten bokstav misslyckas.")]
        public async Task Validate_WhenPasswordMissingLowercase_ShouldFail()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "test@nexapay.se",
                Password = "SECURE1!", // Inga gemener
                Role = Roles.User
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "lösenord utan liten bokstav ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Password" &&
                e.ErrorMessage.Contains("liten bokstav"));
        }

        // --------------------------------------------------------
        // Test 7: Lösenord utan siffra
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Password")]
        [Description(
            "Verifierar att ett lösenord utan siffra misslyckas.")]
        public async Task Validate_WhenPasswordMissingDigit_ShouldFail()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "test@nexapay.se",
                Password = "SecurePass!", // Ingen siffra
                Role = Roles.User
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "lösenord utan siffra ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Password" &&
                e.ErrorMessage.Contains("siffra"));
        }

        // --------------------------------------------------------
        // Test 8: Lösenord utan specialtecken
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Password")]
        [Description(
            "Verifierar att ett lösenord utan specialtecken misslyckas.")]
        public async Task Validate_WhenPasswordMissingSpecialChar_ShouldFail()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "test@nexapay.se",
                Password = "Secure123", // Inget specialtecken
                Role = Roles.User
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "lösenord utan specialtecken ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Password" &&
                e.ErrorMessage.Contains("specialtecken"));
        }

        // --------------------------------------------------------
        // Test 9: Ogiltig roll
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Security")]
        [Description(
            "Verifierar att en roll som inte finns i systemet " +
            "misslyckas validering. " +
            "Förhindrar att okända roller skapas.")]
        public async Task Validate_WhenInvalidRole_ShouldFail()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "test@nexapay.se",
                Password = "Secure1!",
                Role = "SuperAdmin" // Finns inte i systemet
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "en okänd roll ska inte accepteras");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Role");
        }

        // --------------------------------------------------------
        // Test 10: Alla giltiga roller accepteras
        // --------------------------------------------------------
        [TestCase(Roles.Admin)]
        [TestCase(Roles.BankManager)]
        [TestCase(Roles.Teller)]
        [TestCase(Roles.Auditor)]
        [TestCase(Roles.User)]
        [Category("Validation")]
        [Description(
            "Verifierar att alla fem systemroller accepteras av " +
            "RegisterValidator. Roller: Admin, BankManager, " +
            "Teller, Auditor, User.")]
        public async Task Validate_WithAllValidRoles_ShouldPass(string role)
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "test@nexapay.se",
                Password = "Secure1!",
                Role = role
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue(
                $"rollen '{role}' ska vara en giltig systemroll");
        }
    }
}
