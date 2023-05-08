using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebAPIServer.RequestResponse;
using WebAPIServer.DbOperations;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class ClearStage : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;
    readonly IRedisDb _redisDb;

    public ClearStage(ILogger<Login> logger, IGameDb gameDb, IRedisDb redisDb)
    {
        _logger = logger;
        _gameDb = gameDb;
        _redisDb = redisDb;
    }

    [HttpPost]
    public async Task<ClearStageResponse> Post(ClearStageRequest request)
    {
        var response = new ClearStageResponse();
        response.Result = ErrorCode.None;

        (var errorCode, var itemList) = await _redisDb.CheckStageClearAsync(request.UserId, request.StageCode);
        if (errorCode != ErrorCode.None)
        {
            await _redisDb.DeleteStageProgressDataAsync(request.UserId, request.StageCode);

            response.Result = errorCode;
            return response;
        }

        errorCode = await _gameDb.GetStageClearRewardAsync(request.UserId, request.StageCode, request.ClearRank, request.ClearTime, itemList);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        await _redisDb.DeleteStageProgressDataAsync(request.UserId, request.StageCode);

        return response;
    }
}
