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
public class KillEnemy : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IRedisDb _redisDb;

    public KillEnemy(ILogger<Login> logger, IRedisDb redisDb)
    {
        _logger = logger;
        _redisDb = redisDb;
    }

    [HttpPost]
    public async Task<KillEnemyResponse> Post(KillEnemyRequest request)
    {
        var response = new KillEnemyResponse();
        response.Result = ErrorCode.None;

        var errorCode = await _redisDb.KillEnemyAsync(request.UserId, request.StageCode, request.EnemyCode);
        if (errorCode != ErrorCode.None)
        {
            await _redisDb.DeleteStageProgressDataAsync(request.UserId, request.StageCode);

            response.Result = errorCode;
            return response;
        }

        return response;
    }
}
