// ============================================================
// ConcurrencyRetryBehavior.cs – NexaPay.Application/Common/Behaviors
// ============================================================
// När två requests försöker uppdatera samma rad samtidigt
// (samma RowVersion) kastar EF Core en DbUpdateConcurrencyException.
// UnitOfWork översätter den till en domän-egen ConcurrencyException.
//
// Den här behavior fångar undantaget och försöker köra handlern
// igen upp till MaxRetries gånger. UnitOfWork har redan rensat
// EF Cores change-tracker innan den kastade, så nästa försök
// läser om färska data från databasen och har en realistisk
// chans att lyckas.
//
// Pattern: "optimistic concurrency with bounded retry".
// ============================================================

using MediatR;
using NexaPay.Domain.Exceptions;

namespace NexaPay.Application.Common.Behaviors
{
    public class ConcurrencyRetryBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        // Max antal nya försök efter första krocken. 2 ger oss totalt
        // 3 försök innan vi ger upp – tillräckligt för att lösa de
        // flesta naturliga kollisioner utan att gå i evig loop.
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
                    // UnitOfWork har redan rensat change-trackern innan
                    // exception kastades – nästa varv läser fräsch state.
                }
            }
        }
    }
}
