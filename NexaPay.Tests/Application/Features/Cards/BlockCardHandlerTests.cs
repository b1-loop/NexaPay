// ============================================================
// BlockCardHandlerTests.cs
// NexaPay.Tests/Application/Features/Cards
// ============================================================
// Testar BlockCardHandler.
//
// Vi testar:
//   1. Lyckad blockering
//   2. Kort finns inte
//   3. Kort redan blockerat
//   4. Kort är utgånget – kan inte blockeras
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Cards.Commands.BlockCard;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Cards
{
    [TestFixture]
    [Category("Application")]
    [Category("Cards")]
    [Category("BlockCard")]
    public class BlockCardHandlerTests : TestBase
    {
        private BlockCardHandler _handler = null!;

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

            _handler = new BlockCardHandler(MockUnitOfWork.Object);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad blockering
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att ett aktivt kort kan blockeras. " +
            "Status ska ändras till Blocked och SaveChanges ska anropas.")]
        public async Task Handle_WhenCardIsActive_ShouldBlockCard()
        {
            // Arrange
            var card = CreateTestCard(status: CardStatus.Active);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            var command = new BlockCardCommand
            {
                CardId = card.Id,
                Reason = "Misstänkt bedrägeri",
                AdminId = "admin-123"
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "ett aktivt kort ska kunna blockeras");

            card.Status.Should().Be(CardStatus.Blocked,
                "kortets status ska vara Blocked efter blockering");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once,
                "SaveChangesAsync ska anropas exakt en gång");
        }

        // --------------------------------------------------------
        // Test 2: Kort finns inte
        // --------------------------------------------------------
        [Test]
        [Category("NotFound")]
        [Description(
            "Verifierar att blockering misslyckas om kortet inte finns. " +
            "SaveChangesAsync ska aldrig anropas.")]
        public async Task Handle_WhenCardNotFound_ShouldReturnFailure()
        {
            // Arrange
            MockCardRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Card?)null);

            var command = new BlockCardCommand
            {
                CardId = Guid.NewGuid(),
                Reason = "Test",
                AdminId = "admin-123"
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "blockering ska misslyckas om kortet inte hittas");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 3: Kort redan blockerat
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att ett redan blockerat kort inte kan " +
            "blockeras igen. Returnerar Failure med tydligt meddelande.")]
        public async Task Handle_WhenCardAlreadyBlocked_ShouldReturnFailure()
        {
            // Arrange
            var card = CreateTestCard(status: CardStatus.Blocked);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            var command = new BlockCardCommand
            {
                CardId = card.Id,
                Reason = "Dubbelblockeringsförsök",
                AdminId = "admin-123"
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "ett redan blockerat kort ska inte kunna blockeras igen");

            result.Error.Should().Contain("blockerat",
                "felmeddelandet ska nämna att kortet redan är blockerat");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 4: Utgånget kort kan inte blockeras
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att ett utgånget kort inte kan blockeras. " +
            "Utgångna kort är redan inaktiva och behöver inte blockeras.")]
        public async Task Handle_WhenCardIsExpired_ShouldReturnFailure()
        {
            // Arrange
            var card = CreateTestCard(status: CardStatus.Expired);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            var command = new BlockCardCommand
            {
                CardId = card.Id,
                Reason = "Test",
                AdminId = "admin-123"
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "ett utgånget kort ska inte kunna blockeras");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
