using WebAPIServer.DataClass;
using CloudStructures;
using CloudStructures.Structures;
using ZLogger;
using Microsoft.Extensions.Logging;
using SqlKata.Execution;
using StackExchange.Redis;

namespace WebAPIServer.Services;

public class RedisDb : IRedisDb
{
    readonly ILogger<RedisDb> _logger;
    readonly IMasterDb _masterDb;

    RedisConnection _redisConn;

    public RedisDb(ILogger<RedisDb> logger, IConfiguration configuration, IMasterDb masterDb)
    {
        _logger = logger;
        _masterDb = masterDb;

        var RedisAddress = configuration.GetSection("DBConnection")["Redis"];
        var Redisconfig = new RedisConfig("basic", RedisAddress);
        _redisConn = new RedisConnection(Redisconfig);

        _logger.ZLogInformation("Redis Db Connected");
    }

    // 유저 정보 생성
    // accountId로 키밸류 추가
    public async Task<ErrorCode> RegistUserAsync(string email, string authToken, Int64 accountId)
    {
        var uid = "UID_" + accountId;
        var user = new AuthUser
        {
            AuthToken = authToken,
            AccountId = accountId
        };

        try
        {
            var redis = new RedisString<AuthUser>(_redisConn, uid, LoginTimeSpan());
            if (await redis.SetAsync(user, LoginTimeSpan()) == false)
            {
                _logger.ZLogError($"[RegistUser] ErrorCode: {ErrorCode.LoginFailRegistUser}, Email: {email}");
                return ErrorCode.LoginFailRegistUser;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[RegistUser] ErrorCode: {ErrorCode.RegistUserFailException}, Email: {email}");
            return ErrorCode.RegistUserFailException;
        }
    }

    // 유저 정보 가져오기
    // accountId 유저 정보 가져옴
    public async Task<AuthUser> GetUserAsync(Int64 accountid)
    {
        var uid = "UID_" + accountid;

        try
        {
            var redis = new RedisString<AuthUser>(_redisConn, uid, null);
            var user = await redis.GetAsync();
            if (!user.HasValue)
            {
                _logger.ZLogError($"[GetUser] UID:{uid} is Not Assigned User");
                return null;
            }
            return (user.Value);
        }
        catch
        {
            _logger.ZLogError($"[GetUser] UID:{uid} does Not Exist");
            return null;
        }
    }

    // 락걸기
    // 
    public async Task<bool> SetUserReqLockAsync(string userLockKey)
    {
        try
        {
            var redis = new RedisString<AuthUser>(_redisConn, userLockKey, NxKeyTimeSpan());
            if (await redis.SetAsync(new AuthUser { }, NxKeyTimeSpan(), StackExchange.Redis.When.NotExists) == false)
            {
                return true;
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    // 락해제
    public async Task<bool> DelUserReqLockAsync(string userLockKey)
    {
        if (string.IsNullOrEmpty(userLockKey))
        {
            return false;
        }

        try
        {
            var redis = new RedisString<AuthUser>(_redisConn, userLockKey, null);
            var redisResult = await redis.DeleteAsync();
            return redisResult;
        }
        catch
        {
            return false;
        }
    }

    // 공지 가져오기
    // 이게 맞을까...?
    public async Task<Tuple<ErrorCode, byte[]>> NotificationLoading()
    {
        try
        {
            var redis = new RedisString<string>(_redisConn, "notification", null);
            var notificationUrl = await redis.GetAsync();
            if (notificationUrl.Value == null)
            {
                _logger.ZLogError($"[NotificationLoading] ErrorCode: {ErrorCode.NotificationLoadingFailNoUrl}");
                return new Tuple<ErrorCode, byte[]>(ErrorCode.NotificationLoadingFailNoUrl, null);
            }

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(notificationUrl.Value);
                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    return new Tuple<ErrorCode, byte[]>(ErrorCode.None, imageBytes);
                }
                else
                {
                    _logger.ZLogError($"[NotificationLoading] ErrorCode: {ErrorCode.NotificationLoadingFailGetImageFromUrl}");
                    return new Tuple<ErrorCode, byte[]>(ErrorCode.NotificationLoadingFailGetImageFromUrl, null);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[NotificationLoading] ErrorCode: {ErrorCode.NotificationLoadingFailException}");
            return new Tuple<ErrorCode, byte[]>(ErrorCode.NotificationLoadingFailException, null);
        }
    }

    // 스테이지 진행 정보 생성
    // UserId로 키 생성
    public async Task<ErrorCode> CreateStageProgressDataAsync(Int64 userId)
    {
        var stageItemKey = "Stage_" + userId + "_Item";
        var stageEnemyKey = "Stage_" + userId + "_Enemy";

        try
        {
            var item = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageItemKey, null);
            var enemy = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageEnemyKey, null);

            if (await item.SetAsync(null) == false || await enemy.SetAsync(null) == false)
            {
                _logger.ZLogError($"[CreateStageProgressData] ErrorCode: {ErrorCode.StageSelectingFailCreateStageProgressData}, UserId: {userId}");
                return ErrorCode.StageSelectingFailCreateStageProgressData;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[CreateStageProgressData] ErrorCode: {ErrorCode.CreateStageProgressDataFailException}, UserId: {userId}");
            return ErrorCode.CreateStageProgressDataFailException;
        }
    }

    // 스테이지 아이템 획득
    // 유저의 stageItemKey에 아이템 추가
    public async Task<ErrorCode> ObtainItemAsync(Int64 userId, Int64 stageCode, Int64 itemCode, Int64 itemCount)
    {
        var isRightItem = _masterDb.StageItemInfo.Find(i => i.Code == stageCode && i.ItemCode == itemCode);

        if (isRightItem == null)
        {
            _logger.ZLogError($"[ObtainItem] ErrorCode: {ErrorCode.ObtainItemFailWrongItem}, UserId: {userId}");
            return ErrorCode.ObtainItemFailWrongItem;
        }

        var stageItemKey = "Stage_" + userId + "_Item";

        try
        {
            var redis = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageItemKey, null);
            var itemResult = await redis.GetAsync();

            if (itemResult.HasValue == false)
            {
                _logger.ZLogError($"[ObtainItem] ErrorCode: {ErrorCode.ObtainItemFailNoUserData}, UserId: {userId}");
                return ErrorCode.ObtainItemFailNoUserData;
            }

            var itemlist = itemResult.Value;
            var index = itemlist.FindIndex(i => i.Item1 == itemCode);

            if (index >= 0)
            {
                itemlist[index] = new Tuple<Int64, Int64>(itemCode, itemlist[index].Item2 + itemCount);
            }
            else
            {
                itemlist.Add(new Tuple<Int64, Int64>(itemCode, itemCount));
            }

            if (await redis.SetAsync(itemlist) == false)
            {
                _logger.ZLogError($"[ObtainItem] ErrorCode: {ErrorCode.ObtainItemFailRedis}, UserId: {userId}");
                return ErrorCode.ObtainItemFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[ObtainItem] ErrorCode: {ErrorCode.ObtainItemFailException}, UserId: {userId}");
            return ErrorCode.ObtainItemFailException;
        }
    }

    // 스테이지 적 제거 
    // 유저의 stageEnemyKey에 적 추가
    public async Task<ErrorCode> KillEnemyAsync(Int64 userId, Int64 stageCode, Int64 enemyCode)
    {
        var isRightEnemy = _masterDb.StageEnemyInfo.Find(i => i.Code == stageCode && i.NpcCode == enemyCode);

        if (isRightEnemy == null)
        {
            _logger.ZLogError($"[KillEnemy] ErrorCode: {ErrorCode.KillEnemyFailWrongEnemy}, UserId: {userId}");
            return ErrorCode.KillEnemyFailWrongEnemy;
        }

        var stageEnemyKey = "Stage_" + userId + "_Enemy";

        try
        {
            var redis = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageEnemyKey, null);
            var enemyResult = await redis.GetAsync();

            if (enemyResult.HasValue == false)
            {
                _logger.ZLogError($"[KillEnemy] ErrorCode: {ErrorCode.KillEnemyFailNoUserData}, UserId: {userId}");
                return ErrorCode.KillEnemyFailNoUserData;
            }

            var enemylist = enemyResult.Value;
            var index = enemylist.FindIndex(i => i.Item1 == enemyCode);

            if (index >= 0)
            {
                enemylist[index] = new Tuple<Int64, Int64>(enemyCode, enemylist[index].Item2 + 1);
            }
            else
            {
                enemylist.Add(new Tuple<Int64, Int64>(enemyCode, 1));
            }

            if (await redis.SetAsync(enemylist) == false)
            {
                _logger.ZLogError($"[KillEnemy] ErrorCode: {ErrorCode.KillEnemyFailRedis}, UserId: {userId}");

                return ErrorCode.KillEnemyFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[ObtainItem] ErrorCode: {ErrorCode.KillEnemyFailException}, UserId: {userId}");
            return ErrorCode.KillEnemyFailException;
        }
    }

    // 스테이지 클리어 확인
    // MasterData의 StageEnemy 데이터와 redis에 저장해놓은 데이터 비교 
    public async Task<ErrorCode> CheckStageClearAsync(Int64 userId, Int64 stageCode)
    {
        var stageEnemyKey = "Stage_" + userId + "_Enemy";

        try
        {
            var redis = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageEnemyKey, null);
            var enemyResult = await redis.GetAsync();

            if (enemyResult.HasValue == false)
            {
                _logger.ZLogError($"[CheckStageClear] ErrorCode: {ErrorCode.CheckStageClearFailNoUserData}, UserId: {userId}");
                return ErrorCode.CheckStageClearFailNoUserData;
            }

            var enemylist = enemyResult.Value;

            foreach(StageEnemy stageEnemy in _masterDb.StageEnemyInfo)
            {
                if (enemylist.Find(i => i.Item1 == stageEnemy.Code && i.Item2 == stageEnemy.Count) == null)
                {
                    _logger.ZLogError($"[CheckStageClear] ErrorCode: {ErrorCode.CheckStageClearFailWrongData}, UserId: {userId}");
                    return ErrorCode.CheckStageClearFailWrongData;
                }
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[CheckStageClear] ErrorCode: {ErrorCode.CheckStageClearFailException}, UserId: {userId}");
            return ErrorCode.CheckStageClearFailException;
        }
    }

    public TimeSpan LoginTimeSpan()
    {
        return TimeSpan.FromMinutes(RediskeyExpireTime.LoginKeyExpireMin);
    }

    public TimeSpan NxKeyTimeSpan()
    {
        return TimeSpan.FromSeconds(RediskeyExpireTime.NxKeyExpireSecond);
    }
}
