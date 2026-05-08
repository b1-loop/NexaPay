using NexaPay.Application.Common.Interfaces;
using StackExchange.Redis;

namespace NexaPay.Infrastructure.Identity
{
    public class RedisTokenDenylist : ITokenDenylist
    {
        private readonly IDatabase _db;
        private const string Prefix = "nexapay:token:revoked:";

        public RedisTokenDenylist(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public void Revoke(string jti, DateTime expiry)
        {
            var ttl = expiry - DateTime.UtcNow;
            if (ttl <= TimeSpan.Zero)
                return;

            // Redis rensar posten automatiskt när token ändå gått ut
            _db.StringSet(Prefix + jti, 1, ttl);
        }

        public bool IsRevoked(string jti) =>
            _db.KeyExists(Prefix + jti);
    }
}
