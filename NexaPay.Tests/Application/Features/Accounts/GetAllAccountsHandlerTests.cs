// ============================================================
// GetAllAccountsHandlerTests.cs
// NexaPay.Tests/Application/Features/Accounts
// ============================================================
// Testar GetAllAccountsHandler.
//
// Vi testar:
//   1. Vanlig användare → bara egna konton
//   2. Personal → alla konton (inkl. stängda)
//   3. Tom kontolista returnerar tom IEnumerable
//   4. Användarens ID skickas till repository vid User-roll
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Accounts.Queries.GetAllAccounts;
using NexaPay.Domain.Entities;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Accounts
{
    [TestFixture]
    [Category("Application")]
    [Category("Accounts")]
    [Category("GetAllAccounts")]
    public class GetAllAccountsHandlerTests : TestBase
    {
        private GetAllAccountsHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            MockUnitOfWork.Reset();
            MockAccountRepository.Reset();

            MockUnitOfWork.Setup(u => u.Accounts).Returns(MockAccountRepository.Object);

            _handler = new GetAllAccountsHandler(MockUnitOfWork.Object, Mapper);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Vanlig användare ska bara få sina egna konton.")]
        public async Task Handle_WhenUser_ShouldReturnOnlyOwnAccounts()
        {
            var userId = "user-1";
            var ownAccounts = new[]
            {
                CreateTestAccount(ownerId: userId, balance: 100),
                CreateTestAccount(ownerId: userId, balance: 200),
            };

            MockAccountRepository
                .Setup(r => r.GetAccountsByOwnerIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownAccounts);

            var query = new GetAllAccountsQuery
            {
                UserId = userId,
                IsStaff = false
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);

            MockAccountRepository.Verify(
                r => r.GetAccountsByOwnerIdAsync(userId, It.IsAny<CancellationToken>()),
                Times.Once,
                "User-rollen ska bara hämta egna konton");
            MockAccountRepository.Verify(
                r => r.GetAllAccountsIncludingClosedAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "User ska inte hämta alla konton");
        }

        [Test]
        [Category("Security")]
        [Description("Personal ska få alla konton inkl. stängda.")]
        public async Task Handle_WhenStaff_ShouldReturnAllAccountsIncludingClosed()
        {
            var allAccounts = new[]
            {
                CreateTestAccount(ownerId: "user-1", balance: 100),
                CreateTestAccount(ownerId: "user-2", balance: 200),
                CreateTestAccount(ownerId: "user-3", balance: 0),
            };

            MockAccountRepository
                .Setup(r => r.GetAllAccountsIncludingClosedAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(allAccounts);

            var query = new GetAllAccountsQuery
            {
                UserId = "staff-1",
                IsStaff = true
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(3);

            MockAccountRepository.Verify(
                r => r.GetAllAccountsIncludingClosedAsync(It.IsAny<CancellationToken>()),
                Times.Once);
            MockAccountRepository.Verify(
                r => r.GetAccountsByOwnerIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Tom kontolista ska returneras som tom enumerable, inte null.")]
        public async Task Handle_WhenNoAccounts_ShouldReturnEmptyList()
        {
            MockAccountRepository
                .Setup(r => r.GetAccountsByOwnerIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Account>());

            var query = new GetAllAccountsQuery
            {
                UserId = "user-1",
                IsStaff = false
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().BeEmpty();
        }
    }
}
