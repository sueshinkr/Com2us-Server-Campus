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
public class StageList : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;

    public StageList(ILogger<Login> logger, IGameDb gameDb)
    {
        _logger = logger;
        _gameDb = gameDb;
    }

    [HttpPost]
    public async Task<StageListResponse> Post(StageListRequest request)
    {
        var response = new StageListResponse();
        response.Result = ErrorCode.None;

        (var errorCode, response.ClearStage) = await _gameDb.StageListLoadingAsync(request.UserId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        return response;
    }
}
