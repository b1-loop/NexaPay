// ============================================================
// DeleteAccountHandlerTests.cs
// NexaPay.Tests/Application/Features/Accounts
// ============================================================
// Testar DeleteAccountHandler.
//
// Vi testar:
//   1. Lyckad stängning – konto med saldo 0
//   2. Konto finns inte
//   3. Fel ägare – vanlig användare kan inte stänga annans konto
//   4. Admin kan stänga vilket konto som helst
//   5. Konto med saldo > 0 kan inte stängas
//   6. Kontot markeras som inaktivt – inte borttaget
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Accounts.Commands.DeleteAccount;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Accounts
{
    [TestFixture]
    [Category("Application")]
    [Category("Accounts")]
    [Category("DeleteAccount")]
    public class DeleteAccountHandlerTests : TestBase
    {
        private DeleteAccountHandler _handler = null!;

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

            _handler = new DeleteAccountHandler(MockUnitOfWork.Object);
        }

        // --------------------------------------------------------
        // Test 1: Lyckad stängning
        // --------------------------------------------------------
        [Test]
        [Category("HappyPath")]
        [Description(
            "Verifierar att ett tomt konto kan stängas av ägaren. " +
            "Kontot ska markeras som inaktivt och SaveChanges anropas.")]
        public async Task Handle_WhenOwnerClosesEmptyAccount_ShouldSucceed()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                balance: 0);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new DeleteAccountCommand
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "ägaren ska kunna stänga ett tomt konto");

            account.Status.Should().Be(AccountStatus.Closed,
                "kontot ska vara Closed efter stängning");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // --------------------------------------------------------
        // Test 2: Konto finns inte
        // --------------------------------------------------------
        [Test]
        [Category("NotFound")]
        [Description(
            "Verifierar att stängning misslyckas om kontot inte finns.")]
        public async Task Handle_WhenAccountNotFound_ShouldReturnFailure()
        {
            // Arrange
            MockAccountRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NexaPay.Domain.Entities.Account?)null);

            var command = new DeleteAccountCommand
            {
                AccountId = Guid.NewGuid(),
                UserId = "user-123",
                IsStaff = false
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "stängning ska misslyckas om kontot inte hittas");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 3: Fel ägare
        // --------------------------------------------------------
        [Test]
        [Category("Security")]
        [Description(
            "Verifierar att en vanlig användare inte kan stänga " +
            "ett konto som tillhör en annan person.")]
        public async Task Handle_WhenWrongOwner_ShouldReturnFailure()
        {
            // Arrange
            var account = CreateTestAccount(
                ownerId: "user-123",
                balance: 0);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new DeleteAccountCommand
            {
                AccountId = account.Id,
                UserId = "hacker-456",
                IsStaff = false
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "en icke-ägare ska inte kunna stänga kontot");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 4: Admin kan stänga vilket konto som helst
        // --------------------------------------------------------
        [Test]
        [Category("Security")]
        [Description(
            "Verifierar att Admin (IsStaff=true) kan stänga ett " +
            "konto även om de inte är ägaren.")]
        public async Task Handle_WhenAdminClosesAnyAccount_ShouldSucceed()
        {
            // Arrange
            var account = CreateTestAccount(
                ownerId: "customer-123",
                balance: 0);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new DeleteAccountCommand
            {
                AccountId = account.Id,
                UserId = "admin-999",
                IsStaff = true
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue(
                "Admin ska kunna stänga vilket konto som helst");

            account.Status.Should().Be(AccountStatus.Closed,
                "kontot ska vara Closed efter Admin-stängning");
        }

        // --------------------------------------------------------
        // Test 5: Konto med saldo > 0 kan inte stängas
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att ett konto med saldo inte kan stängas. " +
            "Kunden måste tömma kontot först.")]
        public async Task Handle_WhenAccountHasBalance_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                balance: 500);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new DeleteAccountCommand
            {
                AccountId = account.Id,
                UserId = userId,
                IsStaff = false
            };

            // Act
            var result = await _handler.Handle(
                command,
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue(
                "ett konto med saldo ska inte kunna stängas");

            result.Error.Should().Contain("saldo",
                "felmeddelandet ska nämna saldot");

            account.Status.Should().Be(AccountStatus.Open,
                "kontot ska fortfarande vara Open");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // --------------------------------------------------------
        // Test 6: Soft delete – kontot tas inte bort fysiskt
        // --------------------------------------------------------
        [Test]
        [Category("BusinessRule")]
        [Description(
            "Verifierar att kontot markeras som inaktivt (soft delete) " +
            "och inte tas bort fysiskt. Viktigt för revision och historik.")]
        public async Task Handle_WhenAccountClosed_ShouldSoftDelete()
        {
            // Arrange
            var userId = "user-123";
            var account = CreateTestAccount(
                ownerId: userId,
                balance: 0);

            var accountId = account.Id;

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new DeleteAccountCommand
            {
                AccountId = accountId,
                UserId = userId,
                IsStaff = false
            };

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert – kontot finns fortfarande men är inaktivt
            // (Delete-metoden finns inte längre på IAccountRepository –
            //  fysisk radering är omöjlig via interfacet)
            account.Status.Should().Be(AccountStatus.Closed,
                "kontot ska markeras som Closed via Close(), inte raderas fysiskt");

            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once,
                "ändringar ska sparas till databasen");
        }
    }
}
