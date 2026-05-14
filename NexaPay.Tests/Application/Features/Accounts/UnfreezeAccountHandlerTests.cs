// ============================================================
// UnfreezeAccountHandlerTests.cs
// NexaPay.Tests/Application/Features/Accounts
// ============================================================
// Testar UnfreezeAccountHandler.
//
// Vi testar:
//   1. Personal kan avfrysa ett fryst konto
//   2. Konto finns inte → NotFound
//   3. Fel ägare → NotFound
//   4. Öppet konto kan inte avfrysas → Failure
//   5. Stängt konto kan inte avfrysas → Failure
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Accounts.Commands.UnfreezeAccount;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Accounts
{
    [TestFixture]
    [Category("Application")]
    [Category("Accounts")]
    [Category("UnfreezeAccount")]
    public class UnfreezeAccountHandlerTests : TestBase
    {
        private UnfreezeAccountHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            MockUnitOfWork.Reset();
            MockAccountRepository.Reset();

            MockUnitOfWork.Setup(u => u.Accounts).Returns(MockAccountRepository.Object);
            MockUnitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _handler = new UnfreezeAccountHandler(MockUnitOfWork.Object);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Personal ska kunna avfrysa ett fryst konto.")]
        public async Task Handle_WhenStaffUnfreezesFrozenAccount_ShouldSucceed()
        {
            var account = CreateTestAccount(ownerId: "customer-1", balance: 100);
            account.Freeze();

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new UnfreezeAccountCommand
            {
                AccountId = account.Id,
                UserId = "staff-1",
                IsStaff = true
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            account.Status.Should().Be(AccountStatus.Open);
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        [Category("NotFound")]
        [Description("Saknat konto ska returnera NotFound.")]
        public async Task Handle_WhenAccountNotFound_ShouldReturnNotFound()
        {
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Account?)null);

            var command = new UnfreezeAccountCommand
            {
                AccountId = Guid.NewGuid(),
                UserId = "staff-1",
                IsStaff = true
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        [Category("Security")]
        [Description("Annan användare än ägaren ska inte kunna avfrysa kontot.")]
        public async Task Handle_WhenWrongOwner_ShouldReturnNotFound()
        {
            var account = CreateTestAccount(ownerId: "user-1", balance: 0);
            account.Freeze();

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new UnfreezeAccountCommand
            {
                AccountId = account.Id,
                UserId = "hacker-2",
                IsStaff = false
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            account.Status.Should().Be(AccountStatus.Frozen, "kontot ska fortfarande vara fryst");
        }

        [Test]
        [Category("BusinessRule")]
        [Description("Öppet konto kan inte avfrysas.")]
        public async Task Handle_WhenAccountAlreadyOpen_ShouldReturnFailure()
        {
            var userId = "user-1";
            var account = CreateTestAccount(ownerId: userId, balance: 0);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new UnfreezeAccountCommand
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
        [Description("Stängt konto kan inte avfrysas.")]
        public async Task Handle_WhenAccountClosed_ShouldReturnFailure()
        {
            var userId = "user-1";
            var account = CreateTestAccount(ownerId: userId, balance: 0);
            account.Close();

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new UnfreezeAccountCommand
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
        }
    }
}
