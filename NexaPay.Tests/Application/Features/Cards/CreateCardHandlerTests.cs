// ============================================================
// CreateCardHandlerTests.cs
// NexaPay.Tests/Application/Features/Cards
// ============================================================
// Testar CreateCardHandler.
//
// Vi testar:
//   1. Lyckad kortschapning – returnerar CardDto med maskerat nummer
//   2. Konto finns inte
//   3. Fel ägare – kan inte skapa kort på annans konto
//   4. Inaktivt konto – kan inte skapa kort
//   5. Nytt kort skapas alltid med status Inactive
// ============================================================

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NexaPay.Application.Features.Cards.Commands.CreateCard;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Cards
{
    [TestFixture]
    [Category("Application")]
    [Category("Cards")]
    [Category("CreateCard")]
    public class CreateCardHandlerTests : TestBase
    {
        private CreateCardHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            MockUnitOfWork.Reset();
            MockAccountRepository.Reset();
            MockCardRepository.Reset();
            MockTransactionRepository.Reset();

            MockUnitOfWork
                .Setup(u => u.Accounts)
                .Returns(MockAccountRepository.Object);

            MockUnitOfWork
                .Setup(u => u.Cards)
                .Returns(MockCardRepository.Object);

            MockUnitOfWork
                .Setup(u => u.Transactions)
                .Returns(MockTransactionRepository.Object);

            MockUnitOfWork
                .Setup(u => u.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            MockCardRepository
                .Setup(r => r.GetByCardTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Card?)null);

            _handler = new CreateCardHandler(
                MockUnitOfWork.Object,
                Mapper);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad kortskapning
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att ett kort skapas korrekt för ett aktivt konto. " +
            "Kortdto ska innehålla maskerat kortnummer och rätt innehavarnamn.")]
        public async Task Handle_WhenValidRequest_ShouldCreateCard()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(ownerId: userId);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new CreateCardCommand
            {
                AccountId = account.Id,
                CardHolderName = "Anna Svensson",
                UserId = userId
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "ett kort ska skapas för ett aktivt konto med rätt ägare");

            result.Value.Should().NotBeNull();

            result.Value!.Card.CardHolderName.Should().Be("ANNA SVENSSON",
                "kortinnehavarens namn ska konverteras till versaler");

            result.Value.Card.MaskedCardNumber.Should().Contain("****",
                "kortnumret ska vara maskerat i svaret");

            result.Value.CardNumber.Should().HaveLength(16,
                "fullständigt kortnummer ska returneras en gång och aldrig lagras");

            result.Value.Cvv.Should().HaveLength(3,
                "CVV ska vara 3 siffror och returneras en gång vid skapande");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // --------------------------------------------------------
        // Test 2: Konto finns inte
        // --------------------------------------------------------
        [Test]
        [Category("NotFound")]
        [Description(
            "Verifierar att kortskapning misslyckas om kontot inte finns.")]
        public async Task Handle_WhenAccountNotFound_ShouldReturnFailure()
        {
            // Arrange
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Account?)null);

            var command = new CreateCardCommand
            {
                AccountId = Guid.NewGuid(),
                CardHolderName = "Test Testsson",
                UserId = "user-123"
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "kortskapning ska misslyckas om kontot inte hittas");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 3: Fel ägare (IsStaff = false)
        // --------------------------------------------------------
        [Test]
        [Category("Security")]
        [Description(
            "Verifierar att en vanlig användare inte kan skapa kort " +
            "på ett konto som tillhör någon annan.")]
        public async Task Handle_WhenWrongOwnerAndNotStaff_ShouldReturnFailure()
        {
            // Arrange
            var account = CreateTestAccount(ownerId: "user-123");

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new CreateCardCommand
            {
                AccountId = account.Id,
                CardHolderName = "Hacker Person",
                UserId = "hacker-456",
                IsStaff = false
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "man ska inte kunna skapa kort på någon annans konto");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 6: Personal kan skapa kort åt kunder (IsStaff = true)
        // --------------------------------------------------------
        [Test]
        [Category("Security")]
        [Category("Staff")]
        [Description(
            "Verifierar att personal (Teller, BankManager, Admin) " +
            "kan skapa kort på ett konto som tillhör en kund, " +
            "även om de inte är ägaren.")]
        public async Task Handle_WhenStaffCreatesCardForCustomer_ShouldSucceed()
        {
            // Arrange
            var customerId = "customer-123";
            var staffId = "staff-456";
            var account = CreateTestAccount(ownerId: customerId);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new CreateCardCommand
            {
                AccountId = account.Id,
                CardHolderName = "Kund Kundsson",
                UserId = staffId,
                IsStaff = true
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "personal ska kunna skapa kort åt kunder");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once,
                "kortet ska sparas");
        }

        // --------------------------------------------------------
        // Test 4: Inaktivt konto
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att kort inte kan skapas på ett inaktivt konto.")]
        public async Task Handle_WhenAccountInactive_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                isActive: false);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new CreateCardCommand
            {
                AccountId = account.Id,
                CardHolderName = "Test Testsson",
                UserId = userId
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "kort ska inte kunna skapas på ett inaktivt konto");

            result.Error.Should().Contain("inaktivt",
                "felmeddelandet ska nämna att kontot är inaktivt");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 5: Nytt kort skapas alltid som Inactive
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att ett nytt kort alltid skapas med " +
            "status Inactive. Användaren måste aktivera det separat.")]
        public async Task Handle_WhenCardCreated_StatusShouldBeInactive()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(ownerId: userId);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            NexaPay.Domain.Entities.Card? savedCard = null;

            MockCardRepository
                .Setup(r => r.AddAsync(
                    It.IsAny<NexaPay.Domain.Entities.Card>(),
                    It.IsAny<CancellationToken>()))
                .Callback<NexaPay.Domain.Entities.Card, CancellationToken>((c, _) => savedCard = c);

            var command = new CreateCardCommand
            {
                AccountId = account.Id,
                CardHolderName = "Test Testsson",
                UserId = userId
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();

            savedCard.Should().NotBeNull(
                "ett kort ska ha sparats");

            savedCard!.Status.Should().Be(CardStatus.Inactive,
                "nytt kort ska alltid vara Inactive – " +
                "användaren aktiverar det separat");
        }
    }
}
