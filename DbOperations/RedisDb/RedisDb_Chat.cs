using System;
using CloudStructures.Structures;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using StackExchange.Redis;
using WebAPIServer.DataClass;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class RedisDb : IRedisDb
{
    // 로그인시 채팅로비 접속
    //
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

            await lobbyUserRedis.SetAsync(lobbyNum[0], TimeSpan.FromDays(1));

            return new Tuple<ErrorCode, Int64>(ErrorCode.None, lobbyNum[0]);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "EnterChatLobbyFromLogin Exception");

            return new Tuple<ErrorCode, Int64>(ErrorCode.EnterChatLobbyFromLoginFailException, 0);
        }
    }

    // 채팅로비 지정 접속
    //
    public async Task<Tuple<ErrorCode, List<string>>> EnterChatLobbyFromSelectAsync(Int64 userId, Int64 lobbyNum)
    {
        try
        {
            var key = "LobbyList";
            var lobbyListRedis = new RedisSortedSet<Int64>(_redisConn, key, null);

            if (await lobbyListRedis.ScoreAsync(lobbyNum) > 99)
            {
                return new Tuple<ErrorCode, List<string>>(ErrorCode.EnterChatLobbyFromSelectFailLobbyFull, null);
            }
            else
            {
                await lobbyListRedis.IncrementAsync(lobbyNum, 1);
            }

            key = "User_" + userId + "_Lobby";
            var lobbyUserRedis = new RedisString<Int64>(_redisConn, key, null);

            await lobbyUserRedis.SetAsync(lobbyNum, TimeSpan.FromDays(1));

            (var errorCode, var chatHistory) = await ReceiveChatAsync(userId);
            if (errorCode != ErrorCode.None)
            {
                return new Tuple<ErrorCode, List<string>>(errorCode, null);
            }

            return new Tuple<ErrorCode, List<string>>(ErrorCode.None, chatHistory.ToList());
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "EnterChatLobbyFromSelect Exception");

            return new Tuple<ErrorCode, List<string>>(ErrorCode.EnterChatLobbyFromSelectFailException, null);
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
            var lobbyNum = lobbyUserRedisResult.Value;
            if (lobbyNum == 0)
            {
                return ErrorCode.SendChatFailWrongUser;
            }

            key = "Lobby_" + lobbyNum + "_History";
            var lobbyRedis = new RedisSortedSet<string>(_redisConn, key, null);

            var timeStamp = DateTimeOffset.Now;

            await lobbyRedis.AddAsync($"User:{userId},TimeStamp:{timeStamp},Message:{message}", timeStamp.ToUnixTimeSeconds(), TimeSpan.FromDays(1));

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "SendChat Exception");

            return ErrorCode.SendChatFailException;
        }
    }

    // 채팅 메시지 가져오기
    //
    public async Task<Tuple<ErrorCode, List<string>>> ReceiveChatAsync(Int64 userId)
    {
        try
        {
            var key = "User_" + userId + "_Lobby";
            var lobbyUserRedis = new RedisString<Int64>(_redisConn, key, null);

            var lobbyUserRedisResult = await lobbyUserRedis.GetAsync();
            var lobbyNum = lobbyUserRedisResult.Value;
            if (lobbyNum == 0)
            {
                return new Tuple<ErrorCode, List<string>>(ErrorCode.ReceiveChatFailWrongUser, null);
            }

            key = "Lobby_" + lobbyNum + "_History";

            var lobbyRedis = new RedisSortedSet<string>(_redisConn, key, null);

            if (await lobbyRedis.ExistsAsync<RedisSortedSet<string>>() == false)
            {
                return new Tuple<ErrorCode, List<string>>(ErrorCode.None, null);
            }

            var chatHistory = await lobbyRedis.RangeByScoreAsync(order: Order.Descending, take: 50);

            return new Tuple<ErrorCode, List<string>>(ErrorCode.None, chatHistory.ToList());
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "ReceiveChat Exception");

            return new Tuple<ErrorCode, List<string>>(ErrorCode.ReceiveChatFailException, null);
        }
    }
}