using WebAPIServer.DataClass;

namespace WebAPIServer.DbOperations;

public interface IRedisDb
{
    public Task<ErrorCode> CreateUserDataAsync(string email, string authToken, Int64 accountid);
    public Task<AuthUser> GetUserDataAsync(Int64 accountid);
    public Task<bool> SetUserReqLockAsync(string userLockKey);
    public Task<bool> DelUserReqLockAsync(string userLockKey);
    public Task<Tuple<ErrorCode, string>> NotificationLoading();

    public Task<ErrorCode> CreateStageProgressDataAsync(Int64 userId, Int64 stageCode);
    public Task<ErrorCode> DeleteStageProgressDataAsync(Int64 userId, Int64 stageCode);
    public Task<ErrorCode> ObtainItemAsync(Int64 userId, Int64 stageCode, Int64 itemCode, Int64 itemCount);
    public Task<ErrorCode> KillEnemyAsync(Int64 userId, Int64 stageCode, Int64 enemyCode);
    public Task<Tuple<ErrorCode, List<ObtainedStageItem>>> CheckStageClearDataAsync(Int64 userId, Int64 stageCode);
}
