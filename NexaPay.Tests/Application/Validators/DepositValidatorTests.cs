// ============================================================
// DepositValidatorTests.cs
// NexaPay.Tests/Application/Validators
// ============================================================
// Testar DepositValidator – våra valideringsregler.
//
// Validators är enkla att testa eftersom de inte
// behöver någon mock – de tar bara emot ett objekt
// och returnerar valideringsresultat.
//
// Vi testar:
//   1. Giltigt command – ska passera validering
//   2. Negativt belopp – ska misslyckas
//   3. Noll belopp – ska misslyckas
//   4. Tom beskrivning – ska misslyckas
//   5. För stort belopp – ska misslyckas
//   6. Tom AccountId – ska misslyckas
//   7. Tom UserId – ska misslyckas
//   8. Exakt gränsbelopp – ska lyckas
// ============================================================

using FluentAssertions;
using NexaPay.Application.Features.Transactions.Commands.Deposit;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Validators
{
    [TestFixture]
    public class DepositValidatorTests
    {
        private DepositValidator _validator = null!;

        [SetUp]
        public void Setup()
        {
            // Validators har inga beroenden – skapa direkt med new
            // Ingen mock eller DI behövs
            _validator = new DepositValidator();
        }

        // --------------------------------------------------------
        // Test 1: Giltigt command
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att ett helt korrekt DepositCommand " +
            "passerar alla valideringsregler utan fel. " +
            "Används som baseline för att bekräfta att " +
            "validatorn fungerar korrekt i normalfallet.")]
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
                "inga valideringsfel ska finnas för ett giltigt command");
        }

        // --------------------------------------------------------
        // Test 2: Negativt belopp
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att ett negativt belopp (-100) misslyckas " +
            "validering med fel på Amount-fältet. " +
            "Man kan inte sätta in negativa belopp – " +
            "det skulle vara detsamma som ett uttag.")]
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
        [Description(
            "Verifierar att ett belopp på 0 kr misslyckas validering. " +
            "En insättning på 0 kr är meningslös och ska inte tillåtas. " +
            "Validatorn använder GreaterThan(0) för detta krav.")]
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
        [Description(
            "Verifierar att en tom beskrivning misslyckas validering. " +
            "Beskrivningen är obligatorisk för att transaktioner " +
            "ska vara spårbara i kontoutdraget. " +
            "Felet ska vara på Description-fältet.")]
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
        [Description(
            "Verifierar att ett belopp över 1 000 000 kr misslyckas. " +
            "Detta är en AML-regel (Anti Money Laundering) som " +
            "begränsar stora kontanttransaktioner. " +
            "Validatorn använder LessThanOrEqualTo(1000000).")]
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
        [Description(
            "Verifierar att Guid.Empty misslyckas validering. " +
            "Ett tomt AccountId (00000000-0000-...) indikerar " +
            "att något gått fel i controllern. " +
            "Felet ska vara på AccountId-fältet.")]
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
        [Description(
            "Verifierar att ett tomt UserId misslyckas validering. " +
            "UserId sätts från JWT-token i controllern. " +
            "Om den är tom har något gått fel med autentiseringen. " +
            "Felet ska vara på UserId-fältet.")]
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
        [Description(
            "Verifierar att exakt 1 000 000 kr är giltigt. " +
            "Detta testar gränsfallet för LessThanOrEqualTo(1000000). " +
            "Beloppet på exakt gränsen ska tillåtas " +
            "– bara belopp ÖVER gränsen ska nekas.")]
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
                "exakt 1 000 000 kr ska vara giltigt – " +
                "gränsen är LessThanOrEqualTo(1000000)");
        }
    }
}