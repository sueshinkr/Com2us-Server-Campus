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
public class ObtainItem : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IRedisDb _redisDb;
    
    public ObtainItem(ILogger<Login> logger, IRedisDb redisDb)
    {
        _logger = logger;
        _redisDb = redisDb;
    }

    [HttpPost]
    public async Task<ObtainItemResponse> Post(ObtainItemRequest request)
    {
        var response = new ObtainItemResponse();
        response.Result = ErrorCode.None;

        var errorCode = await _redisDb.ObtainItemAsync(request.UserId, request.StageCode, request.ItemCode, request.ItemCount);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        return response;
    }
}
