using WebAPIServer.DataClass;

namespace WebAPIServer.Services;

public interface IRedisDb
{
    public Task<ErrorCode> RegistUserAsync(string email, string authToken, Int64 accountid);
    public Task<AuthUser> GetUserAsync(Int64 accountid);
    public Task<bool> SetUserReqLockAsync(string userLockKey);
    public Task<bool> DelUserReqLockAsync(string userLockKey);
    public Task<Tuple<ErrorCode, byte[]>> NotificationLoading();

    public Task<ErrorCode> CreateStageProgressDataAsync(Int64 userId);
    public Task<ErrorCode> ObtainItemAsync(Int64 userId, Int64 stageCode, Int64 itemCode, Int64 itemCount);
    public Task<ErrorCode> KillEnemyAsync(Int64 userId, Int64 stageCode, Int64 enemyCode);
    public Task<ErrorCode> CheckStageClearAsync(Int64 userId, Int64 stageCode)
}
