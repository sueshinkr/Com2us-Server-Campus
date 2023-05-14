using WebAPIServer.DataClass;
using CloudStructures;
using CloudStructures.Structures;
using ZLogger;
using Microsoft.Extensions.Logging;
using SqlKata.Execution;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

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
    }

    // 채팅로비 리스트 생성
    //
    public async Task<ErrorCode> Init()
    {
        try
        {
            var key = "LobbyList";
            var redis = new RedisSortedSet<Int64>(_redisConn, key, null);

            if (await redis.ExistsAsync<RedisSortedSet<Int64>>() == true)
            {
                return ErrorCode.None;
            }

            var lobbyList = new List<RedisSortedSetEntry<Int64>>();
            for (int i = 1; i <= 100; i++)
            {
                lobbyList.Add(new RedisSortedSetEntry<Int64>(i, 0));
            }

            await redis.AddAsync(lobbyList);

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "Redis Init Exception");

            return ErrorCode.RedisInitFailException;
        }
    }

    // 유저 정보 생성
    // accountId로 키밸류 추가
    public async Task<ErrorCode> CreateUserDataAsync(string email, string authToken, Int64 accountId)
    {
        var uid = "UID_" + accountId;
        var user = new AuthUser
        {
            AuthToken = authToken,
            AccountId = accountId,
            LastLogin = DateTime.Now
        };

        try
        {
            var redis = new RedisString<AuthUser>(_redisConn, uid, LoginTimeSpan());
            if (await redis.SetAsync(user, LoginTimeSpan()) == false)
            {
                return ErrorCode.CreateUserDataFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "CreateUserData Exception");
            
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
                return null;
            }
            return (user.Value);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "GetUserData Exception");
            
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
    public async Task<Tuple<ErrorCode, string>> LoadNotification()
    {
        var notificationUrl = new string("");

        try
        {
            var redis = new RedisString<string>(_redisConn, "notification", null);
            var redisResult = await redis.GetAsync();
            notificationUrl = redisResult.Value;

            if (notificationUrl == null)
            {
                return new Tuple<ErrorCode, string>(ErrorCode.LoadNotificationFailNoUrl, notificationUrl);
            }

            return new Tuple<ErrorCode, string>(ErrorCode.None, notificationUrl);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "LoadNotification Exception");

            return new Tuple<ErrorCode, string>(ErrorCode.LoadNotificationFailException, notificationUrl);
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
