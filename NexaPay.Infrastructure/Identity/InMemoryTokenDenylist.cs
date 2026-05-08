using NexaPay.Application.Common.Interfaces;
using System.Collections.Concurrent;

namespace NexaPay.Infrastructure.Identity
{
    // Singleton – delas över hela applikationens livstid.
    // Tokens som revokeras läggs in här och kontrolleras vid varje request.
    // Innehållet förloras vid omstart – acceptabelt för en 24h token-livstid.
    public class InMemoryTokenDenylist : ITokenDenylist
    {
        private readonly ConcurrentDictionary<string, DateTime> _revoked = new();

        public void Revoke(string jti, DateTime expiry)
        {
            _revoked[jti] = expiry;
            RemoveExpired();
        }

        public bool IsRevoked(string jti) =>
            _revoked.TryGetValue(jti, out var expiry) && expiry > DateTime.UtcNow;

        // Rensa utgångna tokens lazy för att hålla minnet i schack.
        private void RemoveExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var key in _revoked.Keys.ToList())
            {
                if (_revoked.TryGetValue(key, out var exp) && exp <= now)
                    _revoked.TryRemove(key, out _);
            }
        }
    }
}
