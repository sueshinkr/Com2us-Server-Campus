using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebAPIServer.RequestResponse;
using WebAPIServer.Services;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class StageSelect : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;
    readonly IRedisDb _redisDb;

    public StageSelect(ILogger<Login> logger, IGameDb gameDb, IRedisDb redisDb)
    {
        _logger = logger;
        _gameDb = gameDb;
        _redisDb = redisDb;
    }

    [HttpPost]
    public async Task<StageSelectResponse> Post(StageSelectRequest request)
    {
        var response = new StageSelectResponse();
        response.Result = ErrorCode.None;

        (var errorCode, response.stageItem, response.stageEnemy) = await _gameDb.StageSelectingAsync(request.UserId, request.StageNum);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        errorCode = await _redisDb.CreateStageProgressDataAsync(request.UserId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        return response;
    }
}
