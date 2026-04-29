// ============================================================
// CreateAccountValidatorTests.cs
// NexaPay.Tests/Application/Validators
// ============================================================
// Testar CreateAccountValidator – valideringsregler
// för att skapa ett nytt bankkonto.
//
// Vi testar:
//   1. Giltigt command – ska passera
//   2. Tom kontonamn – ska misslyckas
//   3. För kort kontonamn – ska misslyckas
//   4. För långt kontonamn – ska misslyckas
//   5. Exakt min längd – ska lyckas
//   6. Exakt max längd – ska lyckas
//   7. Ogiltig kontotyp – ska misslyckas
//   8. Tom OwnerId – ska misslyckas
//   9. Alla giltiga kontotyper – ska lyckas
// ============================================================

using FluentAssertions;
using NexaPay.Application.Features.Accounts.Commands.CreateAccount;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Validators
{
    [TestFixture]
    [Category("Application")]
    [Category("Validators")]
    [Category("Accounts")]
    public class CreateAccountValidatorTests
    {
        private CreateAccountValidator _validator = null!;

        [SetUp]
        public void Setup()
        {
            // Skapa validator direkt – inga beroenden
            _validator = new CreateAccountValidator();
        }

        // --------------------------------------------------------
        // Test 1: Giltigt command
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att ett helt korrekt CreateAccountCommand " +
            "passerar alla valideringsregler utan fel.")]
        public async Task Validate_WhenValidCommand_ShouldPass()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = "Mitt sparkonto",
                AccountType = AccountType.Savings,
                OwnerId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue(
                "ett giltigt command ska passera alla valideringsregler");

            result.Errors.Should().BeEmpty(
                "inga valideringsfel ska finnas");
        }

        // --------------------------------------------------------
        // Test 2: Tom kontonamn
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("AccountName")]
        [Description(
            "Verifierar att ett tomt kontonamn misslyckas validering. " +
            "Kontonamnet är obligatoriskt – användaren måste " +
            "ge sitt konto ett namn.")]
        public async Task Validate_WhenEmptyAccountName_ShouldFail()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = string.Empty, // Tom!
                AccountType = AccountType.Savings,
                OwnerId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett tomt kontonamn ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "AccountName",
                "felet ska vara på AccountName-fältet");
        }

        // --------------------------------------------------------
        // Test 3: För kort kontonamn
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("AccountName")]
        [Category("EdgeCase")]
        [Description(
            "Verifierar att ett kontonamn med bara 1 tecken " +
            "misslyckas validering. " +
            "MinimumLength(2) kräver minst 2 tecken.")]
        public async Task Validate_WhenAccountNameTooShort_ShouldFail()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = "A", // Bara ett tecken!
                AccountType = AccountType.Checking,
                OwnerId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett kontonamn med bara 1 tecken ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "AccountName",
                "felet ska vara på AccountName-fältet");
        }

        // --------------------------------------------------------
        // Test 4: För långt kontonamn
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("AccountName")]
        [Description(
            "Verifierar att ett kontonamn längre än 100 tecken " +
            "misslyckas validering. " +
            "MaximumLength(100) begränsar längden.")]
        public async Task Validate_WhenAccountNameTooLong_ShouldFail()
        {
            // Arrange
            // Skapa ett namn med 101 tecken – ett för långt
            var tooLongName = new string('A', 101);

            var command = new CreateAccountCommand
            {
                AccountName = tooLongName,
                AccountType = AccountType.Savings,
                OwnerId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett kontonamn längre än 100 tecken ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "AccountName",
                "felet ska vara på AccountName-fältet");
        }

        // --------------------------------------------------------
        // Test 5: Exakt min längd
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("AccountName")]
        [Category("EdgeCase")]
        [Description(
            "Verifierar att ett kontonamn med exakt 2 tecken lyckas. " +
            "Gränsfallet för MinimumLength(2).")]
        public async Task Validate_WhenAccountNameIsMinLength_ShouldPass()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = "AB", // Exakt 2 tecken
                AccountType = AccountType.ISK,
                OwnerId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue(
                "ett kontonamn med exakt 2 tecken ska vara giltigt");
        }

        // --------------------------------------------------------
        // Test 6: Exakt max längd
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("AccountName")]
        [Category("EdgeCase")]
        [Description(
            "Verifierar att ett kontonamn med exakt 100 tecken lyckas. " +
            "Gränsfallet för MaximumLength(100).")]
        public async Task Validate_WhenAccountNameIsMaxLength_ShouldPass()
        {
            // Arrange
            // Skapa ett namn med exakt 100 tecken
            var maxLengthName = new string('A', 100);

            var command = new CreateAccountCommand
            {
                AccountName = maxLengthName,
                AccountType = AccountType.Checking,
                OwnerId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue(
                "ett kontonamn med exakt 100 tecken ska vara giltigt");
        }

        // --------------------------------------------------------
        // Test 7: Ogiltig kontotyp
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("AccountType")]
        [Description(
            "Verifierar att en ogiltig kontotyp misslyckas validering. " +
            "IsInEnum() kontrollerar att värdet finns i AccountType-enum. " +
            "Skyddar mot att klienten skickar in ett ogiltigt heltal.")]
        public async Task Validate_WhenInvalidAccountType_ShouldFail()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = "Testkonto",
                // Cast till AccountType – ett ogiltigt enum-värde
                // 99 finns inte i AccountType-enum
                AccountType = (AccountType)99,
                OwnerId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett ogiltigt enum-värde ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "AccountType",
                "felet ska vara på AccountType-fältet");
        }

        // --------------------------------------------------------
        // Test 8: Tom OwnerId
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Security")]
        [Description(
            "Verifierar att ett tomt OwnerId misslyckas validering. " +
            "OwnerId sätts från JWT-token i controllern. " +
            "Om den är tom har något gått fel med autentiseringen.")]
        public async Task Validate_WhenEmptyOwnerId_ShouldFail()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = "Testkonto",
                AccountType = AccountType.Savings,
                OwnerId = string.Empty // Tom!
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett tomt OwnerId ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "OwnerId",
                "felet ska vara på OwnerId-fältet");
        }

        // --------------------------------------------------------
        // Test 9: Alla giltiga kontotyper
        // --------------------------------------------------------
        [TestCase(AccountType.Checking)]
        [TestCase(AccountType.Savings)]
        [TestCase(AccountType.ISK)]
        [Category("Validation")]
        [Category("AccountType")]
        [Description(
            "Verifierar att alla giltiga kontotyper " +
            "(Checking, Savings, ISK) passerar validering. " +
            "IsInEnum() ska acceptera alla värden i enum.")]
        public async Task Validate_WithAllValidAccountTypes_ShouldPass(
            AccountType accountType)
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                AccountName = "Testkonto",
                AccountType = accountType,
                OwnerId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue(
                $"kontotyp {accountType} ska vara en giltig kontotyp");
        }
    }
}