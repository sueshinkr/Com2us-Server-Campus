using WebAPIServer.ModelDB;

namespace WebAPIServer.Services;

public interface IRedisDb
{
    // 추후 기능 추가 예정
    public Task<ErrorCode> RegistUserAsync(string email, string authToken, Int64 accountid);
    public Task<ErrorCode> VerifyGameDataAsync(double appVersion, double masterDataVersion);
    public Task<AuthUser> GetUserAsync(string accountid);
    public Task<bool> SetUserReqLockAsync(string accountid);
    public Task<bool> DelUserReqLockAsync(string userLockKey);
    public Task<Tuple<ErrorCode, byte[]>> NotificationLoading();
}
