using WebAPIServer.DataClass;
using CloudStructures;
using CloudStructures.Structures;
using ZLogger;
using Microsoft.Extensions.Logging;
using SqlKata.Execution;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using WebAPIServer.Util;

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
            var lobbyListRedis = new RedisSortedSet<Int64>(_redisConn, key, null);

            if (await lobbyListRedis.ExistsAsync<RedisSortedSet<Int64>>() == true)
            {
                return ErrorCode.None;
            }

            var lobbyList = new List<RedisSortedSetEntry<Int64>>();

            for (int i = 1; i <= 100; i++)
            {
                lobbyList.Add(new RedisSortedSetEntry<Int64>(i, 0));
            }

            await lobbyListRedis.AddAsync(lobbyList);

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.RedisInitFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "Redis Init Exception");

            return errorCode;
        }
    }

    // 유저 정보 생성
    // accountId로 키밸류 추가
    public async Task<ErrorCode> CreateUserAuthAsync(string email, string authToken, Int64 accountId)
    {
        var uid = "UID_" + accountId;
        var user = new UserAuth
        {
            AuthToken = authToken,
            AccountId = accountId,
            LastLogin = DateTime.Now
        };

        try
        {
            var userAuthRedis = new RedisString<UserAuth>(_redisConn, uid, LoginTimeSpan());

            if (await userAuthRedis.SetAsync(user, LoginTimeSpan()) == false)
            {
                return ErrorCode.CreateUserAuthFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.CreateUserAuthFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "CreateUserAuth Exception");

            return errorCode;
        }
    }

    // 유저 정보 가져오기
    // accountId 유저 정보 가져옴
    public async Task<UserAuth> GetUserAuthAsync(Int64 accountid)
    {
        var uid = "UID_" + accountid;

        try
        {
            var userAuthRedis = new RedisString<UserAuth>(_redisConn, uid, null);
            var userAuthResult = await userAuthRedis.GetAsync();

            if (!userAuthResult.HasValue)
            {
                return null;
            }

            var userAuth = userAuthResult.Value;

            return (userAuth);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "GetUserAuth Exception");

            return null;
        }
    }

    // 락걸기
    // 
    public async Task<bool> SetUserReqLockAsync(string userLockKey)
    {
        try
        {
            var lockRedis = new RedisString<UserAuth>(_redisConn, userLockKey, NxKeyTimeSpan());

            if (await lockRedis.SetAsync(new UserAuth { }, NxKeyTimeSpan(), StackExchange.Redis.When.NotExists) == false)
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
            var lockRedis = new RedisString<UserAuth>(_redisConn, userLockKey, null);
            var lockRedisResult = await lockRedis.DeleteAsync();
            return lockRedisResult;
        }
        catch
        {
            return false;
        }
    }

    // 공지 가져오기
    // 공지는 Redis에 URL 형태로 저장되어있다고 가정
    public async Task<Tuple<ErrorCode, string>> LoadNotification()
    {
        var notificationUrl = new string("");

        try
        {
            var notificationUrlRedis = new RedisString<string>(_redisConn, "notification", null);
            var notificationUrlRedisResult = await notificationUrlRedis.GetAsync();

            if (notificationUrlRedisResult.HasValue == false)
            {
                return new Tuple<ErrorCode, string>(ErrorCode.LoadNotificationFailNoUrl, notificationUrl);
            }

            notificationUrl = notificationUrlRedisResult.Value;

            return new Tuple<ErrorCode, string>(ErrorCode.None, notificationUrl);
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.LoadNotificationFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "LoadNotification Exception");

            return new Tuple<ErrorCode, string>(errorCode, notificationUrl);
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

    public TimeSpan DungeonKeyTimeSpan()
    {
        return TimeSpan.FromMinutes(RediskeyExpireTime.DungeonKeyExpireMin);
    }
}
