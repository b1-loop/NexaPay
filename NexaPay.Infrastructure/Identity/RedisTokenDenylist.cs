// ============================================================
// RedisTokenDenylist.cs – NexaPay.Infrastructure/Identity
// ============================================================
// Produktionsklar token-revokering som delas mellan flera
// serverinstanser via Redis. Nyckel-prefix "nexapay:token:revoked:"
// + jti, värdet är 1, TTL = återstående token-livstid (då rensas
// posten automatiskt av Redis).
//
// Fail-open: om Redis är otillgängligt loggar vi varning och
// släpper igenom request. Att blockera alla autentiserade
// användare vid Redis-fel skulle ge större skada än enstaka
// revokerade tokens som råkar leva ut sin naturliga exp-tid.
// ============================================================

using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Interfaces;
using StackExchange.Redis;

namespace NexaPay.Infrastructure.Identity
{
    public class RedisTokenDenylist : ITokenDenylist
    {
        private readonly IDatabase _db;
        private readonly ILogger<RedisTokenDenylist> _logger;
        private const string Prefix = "nexapay:token:revoked:";

        public RedisTokenDenylist(IConnectionMultiplexer redis, ILogger<RedisTokenDenylist> logger)
        {
            _db = redis.GetDatabase();
            _logger = logger;
        }

        public void Revoke(string jti, DateTime expiry)
        {
            var ttl = expiry - DateTime.UtcNow;
            if (ttl <= TimeSpan.Zero)
                return;

            try
            {
                // Redis rensar posten automatiskt när token ändå gått ut
                _db.StringSet(Prefix + jti, 1, ttl);
            }
            catch (RedisException ex)
            {
                // Logga men kasta inte – klienten ska få 200 OK på logout.
                // Token löper ut naturligt när dess exp-tid passerar.
                _logger.LogWarning(ex,
                    "Redis otillgängligt vid tokenrevokering (jti={Jti}). " +
                    "Token förblir giltigt tills exp-tiden passerar.", jti);
            }
        }

        public bool IsRevoked(string jti)
        {
            try
            {
                return _db.KeyExists(Prefix + jti);
            }
            catch (RedisException ex)
            {
                // Fail-open: om Redis är nere vet vi inte om token är revokerad.
                // Vi väljer att släppa igenom requests hellre än att blocka alla
                // autentiserade användare. Logga varning för övervakning.
                _logger.LogWarning(ex,
                    "Redis otillgängligt vid kontroll av tokenrevokering (jti={Jti}). " +
                    "Returnerar false (fail-open).", jti);
                return false;
            }
        }
    }
}
