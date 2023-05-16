using System;
using CloudStructures.Structures;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using StackExchange.Redis;
using WebAPIServer.DataClass;
using WebAPIServer.Util;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class RedisDb : IRedisDb
{
    // 로그인시 채팅로비 접속
    // 현재 접속인원이 가장 많은 로비 선택
    public async Task<Tuple<ErrorCode, Int64>> EnterChatLobbyFromLoginAsync(Int64 userId)
    {
        try
        {
            var key = "LobbyList";
            var lobbyListRedis = new RedisSortedSet<Int64>(_redisConn, key, null);
            var lobbyNum = await lobbyListRedis.RangeByScoreAsync(start: 0, stop: 99, take: 1);

            if (lobbyNum.Length == 0)
            {
                return new Tuple<ErrorCode, Int64>(ErrorCode.EnterChatLobbyFromLoginFailLobbyFull, 0);
            }
            else
            {
                await lobbyListRedis.IncrementAsync(lobbyNum[0], 1);
            }

            key = "User_" + userId + "_Lobby";
            var lobbyUserRedis = new RedisString<Int64>(_redisConn, key, null);

            if (await lobbyUserRedis.SetAsync(lobbyNum[0], TimeSpan.FromDays(1)) == false)
            {
                // 롤백
                await lobbyListRedis.DecrementAsync(lobbyNum[0], 1);

                return new Tuple<ErrorCode, Int64>(ErrorCode.EnterChatLobbyFromLoginFailRedis, 0);
            }

            return new Tuple<ErrorCode, Int64>(ErrorCode.None, lobbyNum[0]);
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.EnterChatLobbyFromLoginFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "EnterChatLobbyFromLogin Exception");

            return new Tuple<ErrorCode, Int64>(errorCode, 0);
        }
    }

    // 채팅로비 지정 접속
    //
    public async Task<Tuple<ErrorCode, List<string>>> SelectChatLobbyAsync(Int64 userId, Int64 lobbyNum)
    {
        try
        {
            var key = "User_" + userId + "_Lobby";
            var lobbyUserRedis = new RedisString<Int64>(_redisConn, key, null);
            var lobbyUserRedisResult = await lobbyUserRedis.GetAsync();

            if (lobbyUserRedisResult.HasValue == false)
            {
                return new Tuple<ErrorCode, List<string>>(ErrorCode.SelectChatLobbyFailWrongUser, null);
            }

            var beforeLobbyNum = lobbyUserRedisResult.Value;

            if (beforeLobbyNum == lobbyNum)
            {
                return new Tuple<ErrorCode, List<string>>(ErrorCode.SelectChatLobbyFailAlreadyIn, null);
            }

            key = "LobbyList";
            var lobbyListRedis = new RedisSortedSet<Int64>(_redisConn, key, null);

            if (await lobbyListRedis.ScoreAsync(lobbyNum) > 99)
            {
                return new Tuple<ErrorCode, List<string>>(ErrorCode.SelectChatLobbyFailLobbyFull, null);
            }
            else
            {
                await lobbyListRedis.DecrementAsync(beforeLobbyNum, 1);
                await lobbyListRedis.IncrementAsync(lobbyNum, 1);

                if (await lobbyUserRedis.SetAsync(lobbyNum, TimeSpan.FromDays(1)) == false)
                {
                    // 롤백
                    await lobbyListRedis.IncrementAsync(beforeLobbyNum, 1);
                    await lobbyListRedis.DecrementAsync(lobbyNum, 1);

                    return new Tuple<ErrorCode, List<string>>(ErrorCode.SelectChatLobbyFailRedis, null);
                }
            }

            (var errorCode, var chatHistory) = await ReceiveChatAsync(userId);
            if (errorCode != ErrorCode.None)
            {
                return new Tuple<ErrorCode, List<string>>(errorCode, null);
            }

            return new Tuple<ErrorCode, List<string>>(ErrorCode.None, chatHistory.ToList());
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.SelectChatLobbyFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "SelectChatLobby Exception");

            return new Tuple<ErrorCode, List<string>>(errorCode, null);
        }
    }

    // 채팅 메시지 전송
    //
    public async Task<ErrorCode> SendChatAsync(Int64 userId, string message)
    {
        try
        {
            var key = "User_" + userId + "_Lobby";
            var lobbyUserRedis = new RedisString<Int64>(_redisConn, key, null);

            var lobbyUserRedisResult = await lobbyUserRedis.GetAsync();
            if (lobbyUserRedisResult.HasValue == false)
            {
                return ErrorCode.SendChatFailWrongUser;
            }

            var lobbyNum = lobbyUserRedisResult.Value;
            key = "Lobby_" + lobbyNum + "_History";

            var lobbyRedis = new RedisSortedSet<string>(_redisConn, key, null);

            var timeStamp = DateTimeOffset.Now;

            if (await lobbyRedis.AddAsync($"User:{userId},TimeStamp:{timeStamp},Message:{message}", timeStamp.ToUnixTimeSeconds(), TimeSpan.FromDays(1)) == false)
            {
                return ErrorCode.SendChatFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.SendChatFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "SendChat Exception");

            return errorCode;
        }
    }

    // 채팅 메시지 가져오기
    //
    public async Task<Tuple<ErrorCode, List<string>>> ReceiveChatAsync(Int64 userId)
    {
        var chatHistory = new List<string>();

        try
        {
            var key = "User_" + userId + "_Lobby";
            var lobbyUserRedis = new RedisString<Int64>(_redisConn, key, null);

            var lobbyUserRedisResult = await lobbyUserRedis.GetAsync();
            if (lobbyUserRedisResult.HasValue == false)
            {
                return new Tuple<ErrorCode, List<string>>(ErrorCode.ReceiveChatFailWrongUser, chatHistory);
            }
            
            var lobbyNum = lobbyUserRedisResult.Value;
            key = "Lobby_" + lobbyNum + "_History";

            var lobbyRedis = new RedisSortedSet<string>(_redisConn, key, null);

            if (await lobbyRedis.ExistsAsync<RedisSortedSet<string>>() == false)
            {
                return new Tuple<ErrorCode, List<string>>(ErrorCode.None, chatHistory);
            }

            var chatHistoryArray = await lobbyRedis.RangeByScoreAsync(order: Order.Descending, take: 50);
            chatHistory = chatHistoryArray.ToList();

            return new Tuple<ErrorCode, List<string>>(ErrorCode.None, chatHistory);
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.ReceiveChatFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "ReceiveChat Exception");

            return new Tuple<ErrorCode, List<string>>(errorCode, null);
        }
    }
}