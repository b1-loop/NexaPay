// ============================================================
// DepositValidatorTests.cs
// NexaPay.Tests/Application/Validators
// ============================================================
// Testar DepositValidator – våra valideringsregler.
// ============================================================

using FluentAssertions;
using NexaPay.Application.Features.Transactions.Commands.Deposit;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Validators
{
    [TestFixture]
    [Category("Application")]
    [Category("Validators")]
    [Category("Deposit")]
    public class DepositValidatorTests
    {
        private DepositValidator _validator = null!;

        [SetUp]
        public void Setup()
        {
            _validator = new DepositValidator();
        }

        // --------------------------------------------------------
        // Test 1: Giltigt command
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att ett helt korrekt DepositCommand " +
            "passerar alla valideringsregler utan fel.")]
        public async Task Validate_WhenValidCommand_ShouldPass()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 500,
                Description = "Testinsättning",
                UserId = "user-123"
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
        // Test 2: Negativt belopp
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Amount")]
        [Description(
            "Verifierar att ett negativt belopp misslyckas validering. " +
            "Man kan inte sätta in negativa belopp.")]
        public async Task Validate_WhenNegativeAmount_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = -100,
                Description = "Test",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett negativt belopp ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Amount",
                "felet ska vara på Amount-fältet");
        }

        // --------------------------------------------------------
        // Test 3: Noll belopp
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Amount")]
        [Description(
            "Verifierar att ett belopp på 0 kr misslyckas validering. " +
            "En insättning på 0 kr är meningslös.")]
        public async Task Validate_WhenZeroAmount_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 0,
                Description = "Test",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett belopp på 0 kr ska inte vara giltigt");
        }

        // --------------------------------------------------------
        // Test 4: Tom beskrivning
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Description")]
        [Description(
            "Verifierar att en tom beskrivning misslyckas validering. " +
            "Beskrivningen är obligatorisk för spårbarhet.")]
        public async Task Validate_WhenEmptyDescription_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 500,
                Description = string.Empty,
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "en tom beskrivning ska inte vara giltig");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Description",
                "felet ska vara på Description-fältet");
        }

        // --------------------------------------------------------
        // Test 5: För stort belopp
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Amount")]
        [Category("AML")]
        [Description(
            "Verifierar att ett belopp över 1 000 000 kr misslyckas. " +
            "AML-regel (Anti Money Laundering) – " +
            "begränsar stora kontanttransaktioner.")]
        public async Task Validate_WhenAmountExceedsLimit_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 2000000,
                Description = "Stort belopp",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett belopp över 1 000 000 kr ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Amount",
                "felet ska vara på Amount-fältet");
        }

        // --------------------------------------------------------
        // Test 6: Tom AccountId
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("AccountId")]
        [Description(
            "Verifierar att Guid.Empty misslyckas validering. " +
            "Ett tomt AccountId indikerar ett fel i controllern.")]
        public async Task Validate_WhenEmptyAccountId_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.Empty,
                Amount = 500,
                Description = "Test",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett tomt AccountId ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "AccountId",
                "felet ska vara på AccountId-fältet");
        }

        // --------------------------------------------------------
        // Test 7: Tom UserId
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Security")]
        [Description(
            "Verifierar att ett tomt UserId misslyckas validering. " +
            "UserId sätts från JWT-token – tom = autentiseringsfel.")]
        public async Task Validate_WhenEmptyUserId_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 500,
                Description = "Test",
                UserId = string.Empty
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett tomt UserId ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "UserId",
                "felet ska vara på UserId-fältet");
        }

        // --------------------------------------------------------
        // Test 8: Exakt gränsbelopp
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Amount")]
        [Category("EdgeCase")]
        [Description(
            "Verifierar att exakt 1 000 000 kr är giltigt. " +
            "Gränsfallet för LessThanOrEqualTo(1000000) – " +
            "bara belopp ÖVER gränsen ska nekas.")]
        public async Task Validate_WhenAmountIsExactLimit_ShouldPass()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 1000000,
                Description = "Stort belopp",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue(
                "exakt 1 000 000 kr ska vara giltigt");
        }
    }
}