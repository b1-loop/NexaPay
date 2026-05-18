// ============================================================
// ITokenDenylist.cs – NexaPay.Application/Common/Interfaces
// ============================================================
// JWT är "stateless" – tokens kan inte återkallas direkt.
// Lösningen är en denylist: vid logout sparar vi token-id (jti)
// med dess utgångstid. JWT-middleware kollar denylisten på
// varje request och avvisar revokerade tokens.
//
// Två implementationer i Infrastructure:
//   * InMemoryTokenDenylist – enkel ConcurrentDictionary
//   * RedisTokenDenylist    – delas mellan flera servrar
// ============================================================

namespace NexaPay.Application.Common.Interfaces
{
    public interface ITokenDenylist
    {
        void Revoke(string jti, DateTime expiry);
        bool IsRevoked(string jti);
    }
}
