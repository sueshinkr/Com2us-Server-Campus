using System.ComponentModel.DataAnnotations;
using WebAPIServer.DbOperations;
using WebAPIServer.RequestResponse;
using WebAPIServer.Log;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ZLogger;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class EnterChatLobbyFromSelect : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IRedisDb _redisDb;

    public EnterChatLobbyFromSelect(ILogger<Login> logger, IRedisDb redisDb)
    {
        _logger = logger;
        _redisDb = redisDb;
    }

    [HttpPost]
    public async Task<EnterChatLobbyFromSelectResponse> Post(EnterChatLobbyFromSelectRequest request)
    {
        var response = new EnterChatLobbyFromSelectResponse();
        response.Result = ErrorCode.None;

        (var errorCode, response.ChatHistory) = await _redisDb.EnterChatLobbyFromSelectAsync(request.UserId, request.LobbyNum);
        if (errorCode != ErrorCode.None)
        {
            _logger.ZLogErrorWithPayload(LogManager.MakeEventId(errorCode), new { UserId = request.UserId, LobbyNum = request.LobbyNum }, "EnterChatLobbyFromSelect Error");

            response.Result = errorCode;
            return response;
        }

        return response;
    }
}