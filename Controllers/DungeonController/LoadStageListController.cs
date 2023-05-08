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
public class LoadStageList : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;

    public LoadStageList(ILogger<Login> logger, IGameDb gameDb)
    {
        _logger = logger;
        _gameDb = gameDb;
    }

    [HttpPost]
    public async Task<LoadStageListResponse> Post(LoadStageListRequest request)
    {
        var response = new LoadStageListResponse();
        response.Result = ErrorCode.None;

        (var errorCode, response.ClearStage) = await _gameDb.LoadStageListAsync(request.UserId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        return response;
    }
}
