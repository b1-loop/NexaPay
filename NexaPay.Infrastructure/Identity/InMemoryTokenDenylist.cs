// ============================================================
// InMemoryTokenDenylist.cs – NexaPay.Infrastructure/Identity
// ============================================================
// Single-instance fallback för token-revokering. Bra för
// lokal utveckling och tester men SKALAR INTE horisontellt
// – varje serverinstans har sin egen lista. Vid flera servrar
// måste RedisTokenDenylist användas.
// ============================================================

using NexaPay.Application.Common.Interfaces;
using System.Collections.Concurrent;

namespace NexaPay.Infrastructure.Identity
{
    // Singleton – delas över hela applikationens livstid.
    // Tokens som revokeras läggs in här och kontrolleras vid varje request.
    // Innehållet förloras vid omstart – acceptabelt för en 24h token-livstid.
    // Rensning av utgångna tokens sker var 5:e minut via intern timer (inte vid varje Revoke).
    public class InMemoryTokenDenylist : ITokenDenylist, IDisposable
    {
        private readonly ConcurrentDictionary<string, DateTime> _revoked = new();
        private readonly Timer _cleanupTimer;

        public InMemoryTokenDenylist()
        {
            _cleanupTimer = new Timer(_ => Cleanup(), null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public void Revoke(string jti, DateTime expiry) => _revoked[jti] = expiry;

        public bool IsRevoked(string jti) =>
            _revoked.TryGetValue(jti, out var expiry) && expiry > DateTime.UtcNow;

        private void Cleanup()
        {
            var now = DateTime.UtcNow;
            foreach (var key in _revoked.Keys.ToList())
            {
                if (_revoked.TryGetValue(key, out var exp) && exp <= now)
                    _revoked.TryRemove(key, out _);
            }
        }

        public void Dispose() => _cleanupTimer.Dispose();
    }
}
