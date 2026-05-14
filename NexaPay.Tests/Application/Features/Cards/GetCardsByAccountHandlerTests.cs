// ============================================================
// GetCardsByAccountHandlerTests.cs
// NexaPay.Tests/Application/Features/Cards
// ============================================================
// Testar GetCardsByAccountHandler.
//
// Vi testar:
//   1. Ägaren får sina kort
//   2. Personal får valfritt kontos kort
//   3. Konto saknas → NotFound
//   4. Fel ägare (icke-staff) → NotFound
//   5. Kortnumret är maskerat i DTO (via AutoMapper)
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Cards.Queries.GetCardsByAccount;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Cards
{
    [TestFixture]
    [Category("Application")]
    [Category("Cards")]
    [Category("GetCardsByAccount")]
    public class GetCardsByAccountHandlerTests : TestBase
    {
        private GetCardsByAccountHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            MockUnitOfWork.Reset();
            MockAccountRepository.Reset();
            MockCardRepository.Reset();

            MockUnitOfWork.Setup(u => u.Accounts).Returns(MockAccountRepository.Object);
            MockUnitOfWork.Setup(u => u.Cards).Returns(MockCardRepository.Object);

            _handler = new GetCardsByAccountHandler(MockUnitOfWork.Object, Mapper);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Ägaren ska få tillbaka kontots kort som DTOs.")]
        public async Task Handle_WhenOwnerRequests_ShouldReturnCards()
        {
            var userId = "user-1";
            var account = CreateTestAccount(ownerId: userId, balance: 0);
            var cards = new[]
            {
                CreateTestCard(accountId: account.Id, status: CardStatus.Active),
                CreateTestCard(accountId: account.Id, status: CardStatus.Inactive),
            };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);
            MockCardRepository
                .Setup(r => r.GetCardsByAccountIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cards);

            var query = new GetCardsByAccountQuery
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
        }

        [Test]
        [Category("Security")]
        [Description("Personal ska kunna hämta vilket kontos kort som helst.")]
        public async Task Handle_WhenStaffRequests_ShouldReturnCards()
        {
            var account = CreateTestAccount(ownerId: "customer-1", balance: 0);
            var cards = new[] { CreateTestCard(accountId: account.Id) };

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);
            MockCardRepository
                .Setup(r => r.GetCardsByAccountIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cards);

            var query = new GetCardsByAccountQuery
            {
                AccountId = account.Id,
                UserId = "staff-1",
                IsStaff = true
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(1);
        }

        [Test]
        [Category("NotFound")]
        [Description("Saknat konto ska returnera NotFound.")]
        public async Task Handle_WhenAccountNotFound_ShouldReturnNotFound()
        {
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Account?)null);

            var query = new GetCardsByAccountQuery
            {
                AccountId = Guid.NewGuid(),
                UserId = "user-1",
                IsStaff = false
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            MockCardRepository.Verify(
                r => r.GetCardsByAccountIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        [Category("Security")]
        [Description("Annan användare än ägaren ska få NotFound utan att kort hämtas.")]
        public async Task Handle_WhenWrongOwner_ShouldReturnNotFound()
        {
            var account = CreateTestAccount(ownerId: "user-1", balance: 0);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var query = new GetCardsByAccountQuery
            {
                AccountId = account.Id,
                UserId = "hacker-2",
                IsStaff = false
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            MockCardRepository.Verify(
                r => r.GetCardsByAccountIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never,
                "kort ska inte ens hämtas om personen inte äger kontot");
        }

        [Test]
        [Category("Security")]
        [Description("CardDto ska visa maskerat kortnummer, inte token eller raw last4.")]
        public async Task Handle_ShouldReturnCardWithMaskedNumber()
        {
            var userId = "user-1";
            var account = CreateTestAccount(ownerId: userId, balance: 0);
            var card = CreateTestCard(accountId: account.Id);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);
            MockCardRepository
                .Setup(r => r.GetCardsByAccountIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { card });

            var query = new GetCardsByAccountQuery
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var dto = result.Value!.Single();
            dto.MaskedCardNumber.Should().StartWith("****", "DTO ska visa maskerat kortnummer");
            dto.MaskedCardNumber.Should().EndWith(card.Last4Digits);
        }
    }
}
