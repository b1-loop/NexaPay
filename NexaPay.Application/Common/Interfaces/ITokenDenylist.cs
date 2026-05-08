namespace NexaPay.Application.Common.Interfaces
{
    public interface ITokenDenylist
    {
        void Revoke(string jti, DateTime expiry);
        bool IsRevoked(string jti);
    }
}
