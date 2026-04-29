// ============================================================
// TransferValidatorTests.cs
// NexaPay.Tests/Application/Validators
// ============================================================
// Testar TransferValidator – valideringsregler för överföringar.
//
// Vi testar:
//   1. Giltigt command – ska passera
//   2. Negativt belopp – ska misslyckas
//   3. Noll belopp – ska misslyckas
//   4. För stort belopp – ska misslyckas
//   5. Exakt gränsbelopp – ska lyckas
//   6. Tom FromAccountId – ska misslyckas
//   7. Tom ToAccountId – ska misslyckas
//   8. Samma From och To konto – ska misslyckas
//   9. Tom UserId – ska misslyckas
//   10. Tom beskrivning – ska misslyckas
// ============================================================

using FluentAssertions;
using NexaPay.Application.Features.Transactions.Commands.Transfer;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Validators
{
    [TestFixture]
    [Category("Application")]
    [Category("Validators")]
    [Category("Transfer")]
    public class TransferValidatorTests
    {
        private TransferValidator _validator = null!;

        [SetUp]
        public void Setup()
        {
            _validator = new TransferValidator();
        }

        // --------------------------------------------------------
        // Test 1: Giltigt command
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att ett helt korrekt TransferCommand " +
            "passerar alla valideringsregler utan fel.")]
        public async Task Validate_WhenValidCommand_ShouldPass()
        {
            // Arrange
            var command = new TransferCommand
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = Guid.NewGuid(),
                Amount = 500,
                Description = "Testöverföring",
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
            "Man kan inte överföra negativa belopp.")]
        public async Task Validate_WhenNegativeAmount_ShouldFail()
        {
            // Arrange
            var command = new TransferCommand
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = Guid.NewGuid(),
                Amount = -100, // Negativt!
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
            "En överföring på 0 kr är meningslös.")]
        public async Task Validate_WhenZeroAmount_ShouldFail()
        {
            // Arrange
            var command = new TransferCommand
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = Guid.NewGuid(),
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
        // Test 4: För stort belopp
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Amount")]
        [Category("AML")]
        [Description(
            "Verifierar att ett belopp över 1 000 000 kr misslyckas. " +
            "AML-regel – begränsar stora överföringar.")]
        public async Task Validate_WhenAmountExceedsLimit_ShouldFail()
        {
            // Arrange
            var command = new TransferCommand
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = Guid.NewGuid(),
                Amount = 2000000,
                Description = "Stor överföring",
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
        // Test 5: Exakt gränsbelopp
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Amount")]
        [Category("EdgeCase")]
        [Description(
            "Verifierar att exakt 1 000 000 kr är giltigt. " +
            "Gränsfallet för LessThanOrEqualTo(1000000).")]
        public async Task Validate_WhenAmountIsExactLimit_ShouldPass()
        {
            // Arrange
            var command = new TransferCommand
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = Guid.NewGuid(),
                Amount = 1000000,
                Description = "Stor överföring",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue(
                "exakt 1 000 000 kr ska vara giltigt");
        }

        // --------------------------------------------------------
        // Test 6: Tom FromAccountId
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("AccountId")]
        [Description(
            "Verifierar att ett tomt FromAccountId misslyckas. " +
            "Guid.Empty indikerar ett fel i controllern.")]
        public async Task Validate_WhenEmptyFromAccountId_ShouldFail()
        {
            // Arrange
            var command = new TransferCommand
            {
                FromAccountId = Guid.Empty, // Tom!
                ToAccountId = Guid.NewGuid(),
                Amount = 500,
                Description = "Test",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett tomt FromAccountId ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "FromAccountId",
                "felet ska vara på FromAccountId-fältet");
        }

        // --------------------------------------------------------
        // Test 7: Tom ToAccountId
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("AccountId")]
        [Description(
            "Verifierar att ett tomt ToAccountId misslyckas. " +
            "Guid.Empty indikerar att mottagarkonto saknas.")]
        public async Task Validate_WhenEmptyToAccountId_ShouldFail()
        {
            // Arrange
            var command = new TransferCommand
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = Guid.Empty, // Tom!
                Amount = 500,
                Description = "Test",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "ett tomt ToAccountId ska inte vara giltigt");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "ToAccountId",
                "felet ska vara på ToAccountId-fältet");
        }

        // --------------------------------------------------------
        // Test 8: Samma From och To konto
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att man inte kan överföra pengar " +
            "till samma konto som man tar från. " +
            "FromAccountId och ToAccountId måste vara olika.")]
        public async Task Validate_WhenSameFromAndToAccount_ShouldFail()
        {
            // Arrange
            // Samma Guid för båda kontona!
            var sameAccountId = Guid.NewGuid();

            var command = new TransferCommand
            {
                FromAccountId = sameAccountId,
                ToAccountId = sameAccountId, // Samma konto!
                Amount = 500,
                Description = "Test",
                UserId = "user-123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse(
                "man ska inte kunna överföra pengar till samma konto");

            result.Errors.Should().Contain(e =>
                e.PropertyName == "ToAccountId",
                "felet ska vara på ToAccountId-fältet");
        }

        // --------------------------------------------------------
        // Test 9: Tom UserId
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
            var command = new TransferCommand
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = Guid.NewGuid(),
                Amount = 500,
                Description = "Test",
                UserId = string.Empty // Tom!
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
        // Test 10: Tom beskrivning
        // --------------------------------------------------------
        [Test]
        [Category("Validation")]
        [Category("Description")]
        [Description(
            "Verifierar att en tom beskrivning misslyckas validering. " +
            "Beskrivningen visas i kontoutdraget för båda kontona.")]
        public async Task Validate_WhenEmptyDescription_ShouldFail()
        {
            // Arrange
            var command = new TransferCommand
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = Guid.NewGuid(),
                Amount = 500,
                Description = string.Empty, // Tom!
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
    }
}