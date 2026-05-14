// ============================================================
// UnblockCardHandlerTests.cs
// NexaPay.Tests/Application/Features/Cards
// ============================================================
// Testar UnblockCardHandler.
//
// Vi testar:
//   1. Blockerat kort → kan avblockeras (status Active)
//   2. Kort finns inte → NotFound
//   3. Aktivt kort → Failure (är inte blockerat)
//   4. Inaktivt kort → Failure
//   5. Utgånget kort → Failure
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Cards.Commands.UnblockCard;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Cards
{
    [TestFixture]
    [Category("Application")]
    [Category("Cards")]
    [Category("UnblockCard")]
    public class UnblockCardHandlerTests : TestBase
    {
        private UnblockCardHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            MockUnitOfWork.Reset();
            MockCardRepository.Reset();

            MockUnitOfWork.Setup(u => u.Cards).Returns(MockCardRepository.Object);
            MockUnitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _handler = new UnblockCardHandler(MockUnitOfWork.Object);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Blockerat kort ska kunna avblockeras till status Active.")]
        public async Task Handle_WhenCardBlocked_ShouldUnblock()
        {
            var card = CreateTestCard(status: CardStatus.Blocked);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            var command = new UnblockCardCommand
            {
                CardId = card.Id,
                AdminId = "admin-1"
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            card.Status.Should().Be(CardStatus.Active);
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        [Category("NotFound")]
        [Description("Saknat kort ska returnera NotFound.")]
        public async Task Handle_WhenCardNotFound_ShouldReturnNotFound()
        {
            MockCardRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Card?)null);

            var command = new UnblockCardCommand
            {
                CardId = Guid.NewGuid(),
                AdminId = "admin-1"
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        [Category("BusinessRule")]
        [Description("Aktivt kort kan inte avblockeras (det är inte blockerat).")]
        public async Task Handle_WhenCardActive_ShouldReturnFailure()
        {
            var card = CreateTestCard(status: CardStatus.Active);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            var command = new UnblockCardCommand
            {
                CardId = card.Id,
                AdminId = "admin-1"
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            card.Status.Should().Be(CardStatus.Active);
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        [Category("BusinessRule")]
        [Description("Inaktivt kort kan inte avblockeras.")]
        public async Task Handle_WhenCardInactive_ShouldReturnFailure()
        {
            var card = CreateTestCard(status: CardStatus.Inactive);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            var command = new UnblockCardCommand
            {
                CardId = card.Id,
                AdminId = "admin-1"
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
        }

        [Test]
        [Category("BusinessRule")]
        [Description("Utgånget kort kan inte avblockeras.")]
        public async Task Handle_WhenCardExpired_ShouldReturnFailure()
        {
            var card = CreateTestCard(status: CardStatus.Expired);

            MockCardRepository
                .Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            var command = new UnblockCardCommand
            {
                CardId = card.Id,
                AdminId = "admin-1"
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
        }
    }
}
