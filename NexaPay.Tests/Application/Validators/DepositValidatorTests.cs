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
// ============================================================

using FluentAssertions;
using NexaPay.Application.Features.Transactions.Commands.Deposit;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Validators
{
    [TestFixture]
    public class DepositValidatorTests
    {
        // Validatorn vi testar
        // Ingen mock behövs – validators är självständiga
        private DepositValidator _validator = null!;

        // [SetUp] körs innan varje test
        [SetUp]
        public void Setup()
        {
            // Validators har inga beroenden – skapa direkt med new
            // Ingen mock eller DI behövs
            _validator = new DepositValidator();
        }

        // --------------------------------------------------------
        // Test 1: Giltigt command – ska passera validering
        // --------------------------------------------------------
        [Test]
        public async Task Validate_WhenValidCommand_ShouldPass()
        {
            // Arrange – ett helt giltigt command
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 500,
                Description = "Testinsättning",
                UserId = "user-123"
            };

            // Act – kör validatorn
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue(
                "ett giltigt command ska passera alla valideringsregler");

            result.Errors.Should().BeEmpty(
                "inga valideringsfel ska finnas för ett giltigt command");
        }

        // --------------------------------------------------------
        // Test 2: Negativt belopp – ska misslyckas
        // --------------------------------------------------------
        [Test]
        public async Task Validate_WhenNegativeAmount_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = -100, // Negativt belopp!
                Description = "Test",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett negativt belopp ska inte vara giltigt");

            // Kontrollera att felet är på rätt fält
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Amount",
                "felet ska vara på Amount-fältet");
        }

        // --------------------------------------------------------
        // Test 3: Noll belopp – ska misslyckas
        // --------------------------------------------------------
        [Test]
        public async Task Validate_WhenZeroAmount_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 0, // Noll kr!
                Description = "Test",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett belopp på 0 kr ska inte vara giltigt – " +
                "man kan inte sätta in ingenting");
        }

        // --------------------------------------------------------
        // Test 4: Tom beskrivning – ska misslyckas
        // --------------------------------------------------------
        [Test]
        public async Task Validate_WhenEmptyDescription_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 500,
                Description = string.Empty, // Tom beskrivning!
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
        // Test 5: För stort belopp – ska misslyckas
        // --------------------------------------------------------
        [Test]
        public async Task Validate_WhenAmountExceedsLimit_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 2000000, // Överstiger gränsen på 1 000 000!
                Description = "Stort belopp",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett belopp över 1 000 000 kr ska inte vara giltigt – " +
                "detta är en AML-regel (Anti Money Laundering)");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "Amount",
                "felet ska vara på Amount-fältet");
        }

        // --------------------------------------------------------
        // Test 6: Tom AccountId – ska misslyckas
        // --------------------------------------------------------
        [Test]
        public async Task Validate_WhenEmptyAccountId_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.Empty, // Tom Guid!
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
        // Test 7: Tom UserId – ska misslyckas
        // --------------------------------------------------------
        [Test]
        public async Task Validate_WhenEmptyUserId_ShouldFail()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 500,
                Description = "Test",
                UserId = string.Empty // Tom UserId!
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett tomt UserId ska inte vara giltigt – " +
                "vi måste veta vem som gör insättningen");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "UserId",
                "felet ska vara på UserId-fältet");
        }

        // --------------------------------------------------------
        // Test 8: Exakt gränsbelopp – ska lyckas
        // --------------------------------------------------------
        [Test]
        public async Task Validate_WhenAmountIsExactLimit_ShouldPass()
        {
            // Arrange
            var command = new DepositCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 1000000, // Exakt på gränsen
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