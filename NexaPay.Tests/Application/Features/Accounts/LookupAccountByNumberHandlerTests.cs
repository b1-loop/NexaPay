// ============================================================
// LookupAccountByNumberHandlerTests.cs
// NexaPay.Tests/Application/Features/Accounts
// ============================================================
// Testar LookupAccountByNumberHandler – endpoint för
// förhandsgranskning av mottagarkonto i Transfer-flödet.
//
// Vi testar:
//   1. Konto hittas → returnerar id+namn+kontonummer
//   2. Konto saknas → NotFound
//   3. Lookup-DTO innehåller INTE balance eller ägare
//      (känslig info får inte läcka via öppen lookup)
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Accounts.Queries.LookupAccountByNumber;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Accounts
{
    [TestFixture]
    [Category("Application")]
    [Category("Accounts")]
    [Category("LookupAccountByNumber")]
    public class LookupAccountByNumberHandlerTests : TestBase
    {
        private LookupAccountByNumberHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            MockUnitOfWork.Reset();
            MockAccountRepository.Reset();

            MockUnitOfWork.Setup(u => u.Accounts).Returns(MockAccountRepository.Object);

            _handler = new LookupAccountByNumberHandler(MockUnitOfWork.Object);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Existerande kontonummer ska returnera id, namn och nummer.")]
        public async Task Handle_WhenAccountExists_ShouldReturnLookupDto()
        {
            var account = CreateTestAccount(ownerId: "user-1", balance: 5000);

            MockAccountRepository
                .Setup(r => r.GetByAccountNumberAsync(account.AccountNumber, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var query = new LookupAccountByNumberQuery
            {
                AccountNumber = account.AccountNumber
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.Id.Should().Be(account.Id);
            result.Value.AccountName.Should().Be(account.AccountName);
            result.Value.AccountNumber.Should().Be(account.AccountNumber);
        }

        [Test]
        [Category("NotFound")]
        [Description("Saknat kontonummer ska returnera NotFound.")]
        public async Task Handle_WhenAccountMissing_ShouldReturnNotFound()
        {
            MockAccountRepository
                .Setup(r => r.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Account?)null);

            var query = new LookupAccountByNumberQuery
            {
                AccountNumber = "SE999999"
            };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Value.Should().BeNull();
        }

        [Test]
        [Category("Security")]
        [Description("Lookup-DTO ska inte innehålla saldot eller ägar-ID:t – endast publika fält.")]
        public async Task Handle_WhenAccountExists_LookupDtoShouldNotExposeBalanceOrOwner()
        {
            var account = CreateTestAccount(ownerId: "secret-owner", balance: 12345);

            MockAccountRepository
                .Setup(r => r.GetByAccountNumberAsync(account.AccountNumber, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var query = new LookupAccountByNumberQuery { AccountNumber = account.AccountNumber };

            var result = await _handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            // AccountLookupDto har bara Id, AccountName, AccountNumber – inga andra fält
            // Detta bevisas av typsystemet, men vi verifierar också att inget ovidkommande
            // exponeras via property-skanning.
            var dtoProperties = typeof(AccountLookupDto).GetProperties().Select(p => p.Name).ToArray();
            dtoProperties.Should().BeEquivalentTo(
                new[] { nameof(AccountLookupDto.Id), nameof(AccountLookupDto.AccountName), nameof(AccountLookupDto.AccountNumber) },
                "DTO ska bara exponera publika fält");
        }
    }
}
