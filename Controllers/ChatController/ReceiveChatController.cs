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
public class ReceiveChat : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IRedisDb _redisDb;

    public ReceiveChat(ILogger<Login> logger, IRedisDb redisDb)
    {
        _logger = logger;
        _redisDb = redisDb;
    }

    [HttpPost]
    public async Task<ReceiveChatResponse> Post(ReceiveChatRequest request)
    {
        var response = new ReceiveChatResponse();
        response.Result = ErrorCode.None;

        (var errorCode, response.ChatHistory) = await _redisDb.ReceiveChatAsync(request.LobbyNum);
        if (errorCode != ErrorCode.None)
        {
            _logger.ZLogErrorWithPayload(LogManager.MakeEventId(errorCode), new { UserId = request.UserId, LobbyNum = request.LobbyNum }, "ReceiveChat Error");

            response.Result = errorCode;
            return response;
        }

        return response;
    }
}