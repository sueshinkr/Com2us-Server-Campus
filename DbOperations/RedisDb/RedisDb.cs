using WebAPIServer.DataClass;
using CloudStructures;
using CloudStructures.Structures;
using ZLogger;
using Microsoft.Extensions.Logging;
using SqlKata.Execution;
using StackExchange.Redis;

namespace WebAPIServer.DbOperations;

public partial class RedisDb : IRedisDb
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
    public async Task<ErrorCode> CreateUserDataAsync(string email, string authToken, Int64 accountId)
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
                _logger.ZLogError($"[CreateUserData] ErrorCode: {ErrorCode.CreateUserDataFailRedis}, Email: {email}");
                return ErrorCode.CreateUserDataFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[CreateUserData] ErrorCode: {ErrorCode.CreateUserDataFailException}, Email: {email}");
            return ErrorCode.CreateUserDataFailException;
        }
    }

    // 유저 정보 가져오기
    // accountId 유저 정보 가져옴
    public async Task<AuthUser> GetUserDataAsync(Int64 accountid)
    {
        var uid = "UID_" + accountid;

        try
        {
            var redis = new RedisString<AuthUser>(_redisConn, uid, null);
            var user = await redis.GetAsync();
            if (!user.HasValue)
            {
                _logger.ZLogError($"[GetUserData] UID:{uid} is Not Assigned User");
                return null;
            }
            return (user.Value);
        }
        catch
        {
            _logger.ZLogError($"[GetUserData] UID:{uid} does Not Exist");
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
    public async Task<Tuple<ErrorCode, string>> NotificationLoading()
    {
        var notificationUrl = new string("");

        try
        {
            var redis = new RedisString<string>(_redisConn, "notification", null);
            var redisResult = await redis.GetAsync();
            notificationUrl = redisResult.Value;

            if (notificationUrl == null)
            {
                _logger.ZLogError($"[NotificationLoading] ErrorCode: {ErrorCode.NotificationLoadingFailNoUrl}");
                return new Tuple<ErrorCode, string>(ErrorCode.NotificationLoadingFailNoUrl, notificationUrl);
            }

            return new Tuple<ErrorCode, string>(ErrorCode.None, notificationUrl);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[NotificationLoading] ErrorCode: {ErrorCode.NotificationLoadingFailException}");
            return new Tuple<ErrorCode, string>(ErrorCode.NotificationLoadingFailException, notificationUrl);
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
