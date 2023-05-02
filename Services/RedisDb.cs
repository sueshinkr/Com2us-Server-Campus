using WebAPIServer.DataClass;
using CloudStructures;
using CloudStructures.Structures;
using ZLogger;
using Microsoft.Extensions.Logging;
using SqlKata.Execution;

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
    // Redis에 유저 추가
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
                _logger.ZLogError($"[RegistUserAsync] ErrorCode: {ErrorCode.LoginFailRegistUser}, Email: {email}");

                return ErrorCode.LoginFailRegistUser;
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[RegistUserAsync] ErrorCode: {ErrorCode.RegistUserFailException}, Email: {email}");
            return ErrorCode.RegistUserFailException;
        }

        return ErrorCode.None;
    }

    // 게임 버전 검증
    // MasterData에서 가져온 데이터를 바탕으로 검증
    public ErrorCode VerifyVersionDataAsync(double appVersion, double masterVersion)
    {
        try
        {
            if (_masterDb.VersionDataInfo.AppVersion != appVersion || _masterDb.VersionDataInfo.MasterVersion != masterVersion)
            {
                return ErrorCode.LoginFailGameDataNotMatch;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[VerifyVersionData] ErrorCode: {ErrorCode.VerifyVersionDataFailException}");
            return ErrorCode.VerifyVersionDataFailException;
        }
    }

    public async Task<AuthUser> GetUserAsync(Int64 accountid)
    {
        var uid = "UID_" + accountid;

        try
        {
            var redis = new RedisString<AuthUser>(_redisConn, uid, null);
            var user = await redis.GetAsync();
            if (!user.HasValue)
            {
                _logger.ZLogError($"[GetUserAsync] UID:{uid} is Not Assigned User");
                return null;
            }
            return (user.Value);
        }
        catch
        {
            _logger.ZLogError($"[GetUserAsync] UID:{uid} does Not Exist");
            return null;
        }
    }

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

    public TimeSpan LoginTimeSpan()
    {
        return TimeSpan.FromMinutes(RediskeyExpireTime.LoginKeyExpireMin);
    }

    public TimeSpan NxKeyTimeSpan()
    {
        return TimeSpan.FromSeconds(RediskeyExpireTime.NxKeyExpireSecond);
    }
}
