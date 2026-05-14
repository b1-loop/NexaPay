// ============================================================
// FreezeAccountHandlerTests.cs
// NexaPay.Tests/Application/Features/Accounts
// ============================================================
// Testar FreezeAccountHandler.
//
// Vi testar:
//   1. Personal kan frysa ett öppet konto
//   2. Ägaren kan frysa sitt eget konto
//   3. Konto finns inte → NotFound
//   4. Fel ägare (icke-staff) → NotFound
//   5. Redan fryst konto → Failure
//   6. Stängt konto kan inte frysas → Failure
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Accounts.Commands.FreezeAccount;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Accounts
{
    [TestFixture]
    [Category("Application")]
    [Category("Accounts")]
    [Category("FreezeAccount")]
    public class FreezeAccountHandlerTests : TestBase
    {
        private FreezeAccountHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            MockUnitOfWork.Reset();
            MockAccountRepository.Reset();

            MockUnitOfWork.Setup(u => u.Accounts).Returns(MockAccountRepository.Object);
            MockUnitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _handler = new FreezeAccountHandler(MockUnitOfWork.Object);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Personal ska kunna frysa ett öppet konto.")]
        public async Task Handle_WhenStaffFreezesOpenAccount_ShouldSucceed()
        {
            var account = CreateTestAccount(ownerId: "customer-1", balance: 100);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new FreezeAccountCommand
            {
                AccountId = account.Id,
                UserId = "staff-1",
                IsStaff = true
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue("personal ska kunna frysa konton");
            account.Status.Should().Be(AccountStatus.Frozen);
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Ägaren ska kunna frysa sitt eget konto.")]
        public async Task Handle_WhenOwnerFreezesOwnAccount_ShouldSucceed()
        {
            var userId = "user-1";
            var account = CreateTestAccount(ownerId: userId, balance: 0);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new FreezeAccountCommand
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            account.Status.Should().Be(AccountStatus.Frozen);
        }

        [Test]
        [Category("NotFound")]
        [Description("Saknat konto ska returnera NotFound.")]
        public async Task Handle_WhenAccountNotFound_ShouldReturnNotFound()
        {
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Account?)null);

            var command = new FreezeAccountCommand
            {
                AccountId = Guid.NewGuid(),
                UserId = "user-1",
                IsStaff = false
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        [Category("Security")]
        [Description("Annan användare än ägaren ska inte kunna frysa kontot.")]
        public async Task Handle_WhenWrongOwner_ShouldReturnNotFound()
        {
            var account = CreateTestAccount(ownerId: "user-1", balance: 0);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new FreezeAccountCommand
            {
                AccountId = account.Id,
                UserId = "hacker-2",
                IsStaff = false
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            account.Status.Should().Be(AccountStatus.Open, "kontot ska inte ha frusits");
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        [Category("BusinessRule")]
        [Description("Redan fryst konto kan inte frysas igen.")]
        public async Task Handle_WhenAlreadyFrozen_ShouldReturnFailure()
        {
            var userId = "user-1";
            var account = CreateTestAccount(ownerId: userId, balance: 0);
            account.Freeze();

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new FreezeAccountCommand
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        [Category("BusinessRule")]
        [Description("Stängt konto kan inte frysas.")]
        public async Task Handle_WhenClosedAccount_ShouldReturnFailure()
        {
            var userId = "user-1";
            var account = CreateTestAccount(ownerId: userId, balance: 0);
            account.Close();

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new FreezeAccountCommand
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
