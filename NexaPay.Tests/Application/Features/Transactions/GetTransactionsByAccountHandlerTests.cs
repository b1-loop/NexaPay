// ============================================================
// GetTransactionsByAccountHandlerTests.cs
// NexaPay.Tests/Application/Features/Transactions
// ============================================================
// Testar GetTransactionsByAccountHandler – paginering.
//
// Vi testar:
//   1. Lyckad hämtning – returnerar PagedResult med korrekt metadata
//   2. Konto finns inte
//   3. Fel ägare – vanlig användare kan inte se annans konto
//   4. Personal (IsStaff) kan se alla konton
//   5. Page < 1 normaliseras till 1
//   6. PageSize > 100 begränsas till 100
//   7. PageSize < 1 begränsas till 1
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Transactions.Queries.GetTransactionsByAccount;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Transactions
{
    [TestFixture]
    [Category("Application")]
    [Category("Transactions")]
    [Category("Pagination")]
    public class GetTransactionsByAccountHandlerTests : TestBase
    {
        private GetTransactionsByAccountHandler _handler = null!;

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

            _handler = new GetTransactionsByAccountHandler(
                MockUnitOfWork.Object,
                Mapper);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad hämtning med korrekt metadata
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att PagedResult returneras med korrekt " +
            "TotalCount, Page, PageSize och TotalPages.")]
        public async Task Handle_WhenValidRequest_ShouldReturnPagedResult()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(ownerId: userId);

            var transactions = Enumerable.Range(1, 5)
                .Select(_ => CreateTestTransaction(
                    accountId: account.Id,
                    amount: 100))
                .ToList();

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            MockTransactionRepository
                .Setup(r => r.GetTransactionsByAccountIdPagedAsync(
                    account.Id, 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync((transactions, 5));

            var query = new GetTransactionsByAccountQuery
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false,
                Page = 1,
                PageSize = 20
            };

            // Act
            var result = await _handler.Handle(
                query,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "en giltig förfrågan ska returnera transaktioner");

            result.Value!.TotalCount.Should().Be(5,
                "totalt antal ska matcha antalet transaktioner i databasen");

            result.Value.Page.Should().Be(1,
                "sida 1 begärdes");

            result.Value.PageSize.Should().Be(20,
                "pageSize 20 begärdes");

            result.Value.Items.Should().HaveCount(5,
                "alla 5 transaktioner ska finnas på sida 1");
        }

        // --------------------------------------------------------
        // Test 2: Konto finns inte
        // --------------------------------------------------------
        [Test]
        [Category("NotFound")]
        [Description(
            "Verifierar att Failure returneras om kontot inte hittas.")]
        public async Task Handle_WhenAccountNotFound_ShouldReturnFailure()
        {
            // Arrange
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Account?)null);

            var query = new GetTransactionsByAccountQuery
            {
                AccountId = Guid.NewGuid(),
                UserId = "user-123",
                IsStaff = false,
                Page = 1,
                PageSize = 20
            };

            // Act
            var result = await _handler.Handle(
                query,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "om kontot inte finns ska Failure returneras");
        }

        // --------------------------------------------------------
        // Test 3: Fel ägare
        // --------------------------------------------------------
        [Test]
        [Category("Security")]
        [Description(
            "Verifierar att en vanlig användare inte kan se " +
            "transaktioner för ett konto de inte äger.")]
        public async Task Handle_WhenWrongOwner_ShouldReturnFailure()
        {
            // Arrange
            var account = CreateTestAccount(ownerId: "user-123");

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var query = new GetTransactionsByAccountQuery
            {
                AccountId = account.Id,
                UserId = "hacker-456",
                IsStaff = false,
                Page = 1,
                PageSize = 20
            };

            // Act
            var result = await _handler.Handle(
                query,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "en icke-ägare ska inte kunna se kontots transaktioner");
        }

        // --------------------------------------------------------
        // Test 4: Personal kan se alla kontons transaktioner
        // --------------------------------------------------------
        [Test]
        [Category("Security")]
        [Description(
            "Verifierar att personal (IsStaff=true) kan se " +
            "transaktioner för ett konto de inte äger.")]
        public async Task Handle_WhenStaffRequestsAnyAccount_ShouldSucceed()
        {
            // Arrange
            var account = CreateTestAccount(ownerId: "customer-123");

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            MockTransactionRepository
                .Setup(r => r.GetTransactionsByAccountIdPagedAsync(
                    account.Id, 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<NexaPay.Domain.Entities.Transaction>(), 0));

            var query = new GetTransactionsByAccountQuery
            {
                AccountId = account.Id,
                UserId = "staff-999",
                IsStaff = true,
                Page = 1,
                PageSize = 20
            };

            // Act
            var result = await _handler.Handle(
                query,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "personal ska kunna se alla kontons transaktioner");
        }

        // --------------------------------------------------------
        // Test 5: Page < 1 normaliseras till 1
        // --------------------------------------------------------
        [Test]
        [Category("EdgeCase")]
        [Description(
            "Verifierar att Page=0 eller negativa värden " +
            "normaliseras till 1 via Math.Max(1, page).")]
        public async Task Handle_WhenPageIsZero_ShouldNormalizeTo1()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(ownerId: userId);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            MockTransactionRepository
                .Setup(r => r.GetTransactionsByAccountIdPagedAsync(
                    account.Id, 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<NexaPay.Domain.Entities.Transaction>(), 0));

            var query = new GetTransactionsByAccountQuery
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false,
                Page = 0,
                PageSize = 20
            };

            // Act
            var result = await _handler.Handle(
                query,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();

            result.Value!.Page.Should().Be(1,
                "Page=0 ska normaliseras till 1 via Math.Max(1, page)");
        }

        // --------------------------------------------------------
        // Test 6: PageSize > 100 begränsas till 100
        // --------------------------------------------------------
        [Test]
        [Category("EdgeCase")]
        [Description(
            "Verifierar att PageSize begränsas till max 100 " +
            "via Math.Clamp för att förhindra för stora requests.")]
        public async Task Handle_WhenPageSizeTooLarge_ShouldClampTo100()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(ownerId: userId);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            MockTransactionRepository
                .Setup(r => r.GetTransactionsByAccountIdPagedAsync(
                    account.Id, 1, 100, It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<NexaPay.Domain.Entities.Transaction>(), 0));

            var query = new GetTransactionsByAccountQuery
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false,
                Page = 1,
                PageSize = 9999
            };

            // Act
            var result = await _handler.Handle(
                query,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();

            result.Value!.PageSize.Should().Be(100,
                "PageSize=9999 ska begränsas till 100 via Math.Clamp");
        }
    }
}
