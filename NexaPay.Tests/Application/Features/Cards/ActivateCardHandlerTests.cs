// ============================================================
// ActivateCardHandlerTests.cs
// NexaPay.Tests/Application/Features/Cards
// ============================================================
// Testar ActivateCardHandler.
//
// Vi testar:
//   1. Lyckad aktivering av eget Inactive-kort
//   2. Personal (IsStaff) kan aktivera kundens kort
//   3. Fel ägare – kan inte aktivera annans kort
//   4. Kort finns inte
//   5. Kort är redan aktivt
//   6. Blockerat kort kan inte aktiveras
//   7. Utgånget kort kan inte aktiveras
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Cards.Commands.ActivateCard;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Cards
{
    [TestFixture]
    [Category("Application")]
    [Category("Cards")]
    [Category("ActivateCard")]
    public class ActivateCardHandlerTests : TestBase
    {
        private ActivateCardHandler _handler = null!;

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

            _handler = new ActivateCardHandler(MockUnitOfWork.Object);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad aktivering av eget kort
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att en kortinnehavare kan aktivera sitt " +
            "eget Inactive-kort. Status ska bli Active.")]
        public async Task Handle_WhenOwnerActivatesInactiveCard_ShouldSucceed()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(ownerId: userId);
            var card = CreateTestCard(
                accountId: account.Id,
                status: CardStatus.Inactive);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new ActivateCardCommand
            {
                CardId = card.Id,
                UserId = userId,
                IsStaff = false
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "kortinnehavaren ska kunna aktivera sitt eget kort");

            card.Status.Should().Be(CardStatus.Active,
                "kortets status ska vara Active efter aktivering");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // --------------------------------------------------------
        // Test 2: Personal kan aktivera kundens kort
        // --------------------------------------------------------
        [Test]
        [Category("Security")]
        [Description(
            "Verifierar att personal (IsStaff=true) kan aktivera " +
            "en kunds kort utan att äga kontot. " +
            "Ägarskapskontrollen ska hoppas över för personal.")]
        public async Task Handle_WhenStaffActivatesCustomerCard_ShouldSucceed()
        {
            // Arrange
            var account = CreateTestAccount(ownerId: "customer-123");
            var card = CreateTestCard(
                accountId: account.Id,
                status: CardStatus.Inactive);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            var command = new ActivateCardCommand
            {
                CardId = card.Id,
                UserId = "staff-456",
                IsStaff = true
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "personal ska kunna aktivera kunders kort");

            card.Status.Should().Be(CardStatus.Active,
                "kortets status ska vara Active");

            MockAccountRepository.Verify(
                r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never,
                "ägarskapskontrollen ska hoppas över för personal");
        }

        // --------------------------------------------------------
        // Test 3: Fel ägare
        // --------------------------------------------------------
        [Test]
        [Category("Security")]
        [Description(
            "Verifierar att en användare inte kan aktivera ett kort " +
            "som tillhör ett konto de inte äger.")]
        public async Task Handle_WhenWrongOwner_ShouldReturnFailure()
        {
            // Arrange
            var account = CreateTestAccount(ownerId: "user-123");
            var card = CreateTestCard(
                accountId: account.Id,
                status: CardStatus.Inactive);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new ActivateCardCommand
            {
                CardId = card.Id,
                UserId = "hacker-456",
                IsStaff = false
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "en icke-ägare ska inte kunna aktivera kortet");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 4: Kort finns inte
        // --------------------------------------------------------
        [Test]
        [Category("NotFound")]
        [Description(
            "Verifierar att aktivering misslyckas om kortet inte finns.")]
        public async Task Handle_WhenCardNotFound_ShouldReturnFailure()
        {
            // Arrange
            MockCardRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Card?)null);

            var command = new ActivateCardCommand
            {
                CardId = Guid.NewGuid(),
                UserId = "user-123",
                IsStaff = false
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "aktivering ska misslyckas om kortet inte hittas");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 5: Kort är redan aktivt
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att ett redan aktivt kort inte kan aktiveras igen.")]
        public async Task Handle_WhenCardAlreadyActive_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(ownerId: userId);
            var card = CreateTestCard(
                accountId: account.Id,
                status: CardStatus.Active);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new ActivateCardCommand
            {
                CardId = card.Id,
                UserId = userId,
                IsStaff = false
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "ett redan aktivt kort ska inte kunna aktiveras igen");

            result.Error.Should().Contain("aktivt",
                "felmeddelandet ska nämna att kortet redan är aktivt");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 6: Blockerat kort kan inte aktiveras
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att ett blockerat kort inte kan aktiveras. " +
            "Blockerade kort måste avblockeras av Admin först.")]
        public async Task Handle_WhenCardIsBlocked_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(ownerId: userId);
            var card = CreateTestCard(
                accountId: account.Id,
                status: CardStatus.Blocked);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new ActivateCardCommand
            {
                CardId = card.Id,
                UserId = userId,
                IsStaff = false
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "ett blockerat kort ska inte kunna aktiveras");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 7: Utgånget kort kan inte aktiveras
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att ett utgånget kort inte kan aktiveras.")]
        public async Task Handle_WhenCardIsExpired_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(ownerId: userId);
            var card = CreateTestCard(
                accountId: account.Id,
                status: CardStatus.Expired);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new ActivateCardCommand
            {
                CardId = card.Id,
                UserId = userId,
                IsStaff = false
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "ett utgånget kort ska inte kunna aktiveras");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
