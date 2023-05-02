using WebAPIServer.DataClass;

namespace WebAPIServer.Services;

public interface IRedisDb
{
    // 추후 기능 추가 예정
    public Task<ErrorCode> RegistUserAsync(string email, string authToken, Int64 accountid);
    public Task<AuthUser> GetUserAsync(Int64 accountid);
    public Task<bool> SetUserReqLockAsync(string userLockKey);
    public Task<bool> DelUserReqLockAsync(string userLockKey);
    public Task<Tuple<ErrorCode, byte[]>> NotificationLoading();
}
