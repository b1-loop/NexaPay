using MediatR;
using NexaPay.Domain.Exceptions;

namespace NexaPay.Application.Common.Behaviors
{
    public class ConcurrencyRetryBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private const int MaxRetries = 2;

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var attempt = 0;
            while (true)
            {
                try
                {
                    return await next();
                }
                catch (ConcurrencyException) when (attempt++ < MaxRetries)
                {
                    // UnitOfWork cleared the change tracker before throwing,
                    // so the next attempt re-reads fresh entity state from the DB.
                }
            }
        }
    }
}
