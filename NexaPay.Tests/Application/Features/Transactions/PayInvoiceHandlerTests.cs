// ============================================================
// PayInvoiceHandlerTests.cs
// NexaPay.Tests/Application/Features/Transactions
// ============================================================
// Testar PayInvoiceHandler.
//
// Vi testar:
//   1. Lyckad fakturabetalning minskar saldot
//   2. Konto saknas → NotFound
//   3. Fel ägare (icke-staff) → NotFound
//   4. För lite saldo → Failure
//   5. Idempotency-Key: andra gången returneras befintlig transaktion
//   6. Personal kan betala faktura från kunders konto
// ============================================================

using FluentAssertions;
using Moq;
using NexaPay.Application.Features.Transactions.Commands.PayInvoice;
using NexaPay.Domain.Enums;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Features.Transactions
{
    [TestFixture]
    [Category("Application")]
    [Category("Transactions")]
    [Category("PayInvoice")]
    public class PayInvoiceHandlerTests : TestBase
    {
        private PayInvoiceHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            MockUnitOfWork.Reset();
            MockAccountRepository.Reset();
            MockTransactionRepository.Reset();

            MockUnitOfWork.Setup(u => u.Accounts).Returns(MockAccountRepository.Object);
            MockUnitOfWork.Setup(u => u.Transactions).Returns(MockTransactionRepository.Object);
            MockUnitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _handler = new PayInvoiceHandler(MockUnitOfWork.Object, Mapper);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Lyckad fakturabetalning ska minska saldot och skapa en transaktion.")]
        public async Task Handle_WhenValidPayment_ShouldDecreaseBalance()
        {
            var userId = "user-1";
            var account = CreateTestAccount(ownerId: userId, balance: 1000);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new PayInvoiceCommand
            {
                AccountId = account.Id,
                Amount = 250,
                Bankgiro = "12345678",
                Ocr = "123456789",
                Description = "Elräkning mars",
                UserId = userId,
                IsStaff = false
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            account.Balance.Amount.Should().Be(750, "1000 - 250 = 750");
            result.Value!.Type.Should().Be(TransactionType.InvoicePayment.ToString());
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

            var command = new PayInvoiceCommand
            {
                AccountId = Guid.NewGuid(),
                Amount = 100,
                Bankgiro = "12345678",
                Ocr = "123456789",
                Description = "Test",
                UserId = "user-1",
                IsStaff = false
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        [Category("Security")]
        [Description("Fel ägare ska få NotFound och saldot ska förbli oförändrat.")]
        public async Task Handle_WhenWrongOwner_ShouldReturnNotFound()
        {
            var account = CreateTestAccount(ownerId: "user-1", balance: 1000);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new PayInvoiceCommand
            {
                AccountId = account.Id,
                Amount = 100,
                Bankgiro = "12345678",
                Ocr = "123456789",
                Description = "Test",
                UserId = "hacker-2",
                IsStaff = false
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            account.Balance.Amount.Should().Be(1000, "saldot ska inte ha minskats");
            MockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        [Category("BusinessRule")]
        [Description("För lite saldo ska resultera i Failure.")]
        public async Task Handle_WhenInsufficientBalance_ShouldReturnFailure()
        {
            var userId = "user-1";
            var account = CreateTestAccount(ownerId: userId, balance: 50);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new PayInvoiceCommand
            {
                AccountId = account.Id,
                Amount = 200,
                Bankgiro = "12345678",
                Ocr = "123456789",
                Description = "Test",
                UserId = userId,
                IsStaff = false
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Should().Contain("saldo");
            account.Balance.Amount.Should().Be(50);
        }

        [Test]
        [Category("Idempotency")]
        [Description("Samma Idempotency-Key ska returnera den befintliga transaktionen utan att betala igen.")]
        public async Task Handle_WhenDuplicateIdempotencyKey_ShouldReturnExistingTransaction()
        {
            var userId = "user-1";
            var account = CreateTestAccount(ownerId: userId, balance: 1000);
            var idempotencyKey = Guid.NewGuid();
            var existing = CreateTestTransaction(accountId: account.Id, amount: 100);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);
            MockTransactionRepository
                .Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

            var command = new PayInvoiceCommand
            {
                AccountId = account.Id,
                Amount = 100,
                Bankgiro = "12345678",
                Ocr = "123456789",
                Description = "Test",
                UserId = userId,
                IsStaff = false,
                IdempotencyKey = idempotencyKey
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            account.Balance.Amount.Should().Be(1000, "duplicate request ska inte dra pengar igen");
            MockUnitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "SaveChanges ska inte anropas vid duplicate idempotency-key");
        }

        [Test]
        [Category("Security")]
        [Description("Personal ska kunna betala faktura från kunders konto.")]
        public async Task Handle_WhenStaffPaysForCustomer_ShouldSucceed()
        {
            var account = CreateTestAccount(ownerId: "customer-1", balance: 500);

            MockAccountRepository
                .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            var command = new PayInvoiceCommand
            {
                AccountId = account.Id,
                Amount = 100,
                Bankgiro = "12345678",
                Ocr = "123456789",
                Description = "Personal-betalning",
                UserId = "staff-1",
                IsStaff = true
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            account.Balance.Amount.Should().Be(400);
        }
    }
}
