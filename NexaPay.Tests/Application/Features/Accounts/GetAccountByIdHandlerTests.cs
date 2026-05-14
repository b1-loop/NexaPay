// ============================================================
// GetAccountByIdHandlerTests.cs
// NexaPay.Tests/Application/Features/Accounts
// ============================================================
// Testar GetAccountByIdHandler.
//
// Vi testar:
//   1. Ägaren får tillbaka sitt konto
//   2. Personal får valfritt konto (inkl. stängda)
//   3. Konto finns inte → NotFound
//   4. Fel ägare (icke-staff) → NotFound (döljer existensen)
//   5. DTO innehåller rätt fält
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Accounts.Queries.GetAccountById;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Accounts
{
    [TestFixture]
    [Category("Application")]
    [Category("Accounts")]
    [Category("GetAccountById")]
    public class GetAccountByIdHandlerTests : TestBase
    {
        private GetAccountByIdHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            MockUnitOfWork.Reset();
            MockAccountRepository.Reset();

            MockUnitOfWork.Setup(u => u.Accounts).Returns(MockAccountRepository.Object);

            _handler = new GetAccountByIdHandler(MockUnitOfWork.Object, Mapper);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Ägaren ska få tillbaka sitt eget konto som DTO.")]
        public async Task Handle_WhenOwnerRequests_ShouldReturnAccount()
        {
            var userId = "user-1";
            var account = CreateTestAccount(ownerId: userId, balance: 250);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var query = new GetAccountByIdQuery
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.Id.Should().Be(account.Id);
            result.Value.OwnerId.Should().Be(userId);
            result.Value.Balance.Should().Be(250);
        }

        [Test]
        [Category("Security")]
        [Description("Personal ska kunna hämta valfritt konto, inkl. stängda.")]
        public async Task Handle_WhenStaffRequests_ShouldUseIncludingClosed()
        {
            var account = CreateTestAccount(ownerId: "customer-1", balance: 0);

            MockAccountRepository
                .Setup(r => r.GetByIdIncludingClosedAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var query = new GetAccountByIdQuery
            {
                AccountId = account.Id,
                UserId = "staff-1",
                IsStaff = true
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            MockAccountRepository.Verify(
                r => r.GetByIdIncludingClosedAsync(account.Id, It.IsAny<CancellationToken>()),
                Times.Once,
                "personal ska gå mot endpoint som inkluderar stängda konton");
        }

        [Test]
        [Category("NotFound")]
        [Description("Saknat konto ska returnera NotFound.")]
        public async Task Handle_WhenAccountNotFound_ShouldReturnNotFound()
        {
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Account?)null);

            var query = new GetAccountByIdQuery
            {
                AccountId = Guid.NewGuid(),
                UserId = "user-1",
                IsStaff = false
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Value.Should().BeNull();
        }

        [Test]
        [Category("Security")]
        [Description("Annan användare ska få NotFound (information leak protection).")]
        public async Task Handle_WhenWrongOwner_ShouldReturnNotFound()
        {
            var account = CreateTestAccount(ownerId: "user-1", balance: 0);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var query = new GetAccountByIdQuery
            {
                AccountId = account.Id,
                UserId = "hacker-2",
                IsStaff = false
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Should().Contain("hittades inte", "felmeddelandet ska dölja att kontot existerar");
        }
    }
}
