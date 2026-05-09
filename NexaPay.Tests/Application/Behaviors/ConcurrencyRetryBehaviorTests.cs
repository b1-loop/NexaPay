using FluentAssertions;
using MediatR;
using NexaPay.Application.Common.Behaviors;
using NexaPay.Domain.Exceptions;
using NUnit.Framework;

namespace NexaPay.Tests.Application.Behaviors
{
    [TestFixture]
    [Category("Application")]
    [Category("Behaviors")]
    public class ConcurrencyRetryBehaviorTests
    {
        private ConcurrencyRetryBehavior<FakeRequest, FakeResponse> _behavior = null!;

        [SetUp]
        public void Setup()
        {
            _behavior = new ConcurrencyRetryBehavior<FakeRequest, FakeResponse>();
        }

        // --------------------------------------------------------
        // Test 1: Lyckat retry efter ett ConcurrencyException
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att ett enda ConcurrencyException triggar ett retry " +
            "och att det andra försöket returnerar resultatet.")]
        public async Task Handle_WhenConcurrencyExceptionThrownOnce_RetriesAndSucceeds()
        {
            var callCount = 0;
            RequestHandlerDelegate<FakeResponse> next = _ =>
            {
                if (++callCount == 1)
                    throw new ConcurrencyException("conflict", new Exception());
                return Task.FromResult(new FakeResponse());
            };

            var result = await _behavior.Handle(
                new FakeRequest(), next, CancellationToken.None);

            result.Should().NotBeNull();
            callCount.Should().Be(2,
                "ett ConcurrencyException ska trigga exakt ett retry");
        }

        // --------------------------------------------------------
        // Test 2: Lyckat retry efter två ConcurrencyExceptions
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att beteendet klarar upp till MaxRetries (2) retries " +
            "och lyckas på det tredje och sista försöket.")]
        public async Task Handle_WhenConcurrencyExceptionThrownTwice_RetriesAndSucceeds()
        {
            var callCount = 0;
            RequestHandlerDelegate<FakeResponse> next = _ =>
            {
                if (++callCount < 3)
                    throw new ConcurrencyException("conflict", new Exception());
                return Task.FromResult(new FakeResponse());
            };

            var result = await _behavior.Handle(
                new FakeRequest(), next, CancellationToken.None);

            result.Should().NotBeNull();
            callCount.Should().Be(3,
                "MaxRetries = 2 innebär 3 totala försök – det tredje ska lyckas");
        }

        // --------------------------------------------------------
        // Test 3: Ger upp och kastar vidare efter för många retries
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att ConcurrencyException kastas vidare när antalet " +
            "retries har överskridits (MaxRetries = 2, dvs 3 totala försök).")]
        public async Task Handle_WhenConcurrencyExceptionExceedsMaxRetries_ThrowsConcurrencyException()
        {
            var callCount = 0;
            RequestHandlerDelegate<FakeResponse> next = _ =>
            {
                callCount++;
                throw new ConcurrencyException("conflict", new Exception());
            };

            var act = async () => await _behavior.Handle(
                new FakeRequest(), next, CancellationToken.None);

            await act.Should().ThrowAsync<ConcurrencyException>(
                "efter 3 misslyckade försök ska ConcurrencyException kastas till anroparen");

            callCount.Should().Be(3,
                "beteendet ska göra exakt 3 försök: original + 2 retries");
        }

        // --------------------------------------------------------
        // Test 4: Andra exceptions triggar inte retry
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att en annan exception än ConcurrencyException " +
            "kastas direkt utan att trigga retry-logiken.")]
        public async Task Handle_WhenOtherExceptionIsThrown_DoesNotRetry()
        {
            var callCount = 0;
            RequestHandlerDelegate<FakeResponse> next = _ =>
            {
                callCount++;
                throw new InvalidOperationException("inte ett concurrency-fel");
            };

            var act = async () => await _behavior.Handle(
                new FakeRequest(), next, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>(
                "beteendet ska inte fånga andra exceptions än ConcurrencyException");

            callCount.Should().Be(1,
                "retry ska inte triggas – anropet ska ske exakt en gång");
        }

        // --------------------------------------------------------
        // Test 5: Ingen exception – resultatet returneras direkt
        // --------------------------------------------------------
        [Test]
        [Description(
            "Verifierar att beteendet returnerar handlens svar direkt " +
            "utan extra anrop när inget exception kastas.")]
        public async Task Handle_WhenNoException_ReturnsResultImmediately()
        {
            var callCount = 0;
            RequestHandlerDelegate<FakeResponse> next = _ =>
            {
                callCount++;
                return Task.FromResult(new FakeResponse());
            };

            var result = await _behavior.Handle(
                new FakeRequest(), next, CancellationToken.None);

            result.Should().NotBeNull();
            callCount.Should().Be(1,
                "utan exception ska handlens svar returneras på första försöket");
        }

        // Minimala dummy-typer för det generiska beteendet
        private class FakeRequest : IRequest<FakeResponse> { }
        private class FakeResponse { }
    }
}
