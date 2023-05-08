using System.ComponentModel.DataAnnotations;
using WebAPIServer.DbOperations;
using WebAPIServer.RequestResponse;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ZLogger;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class EnhanceItem : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;

    public EnhanceItem(ILogger<Login> logger, IGameDb gameDb)
    {
        _logger = logger;
        _gameDb = gameDb;
    }

    [HttpPost]
    public async Task<EnhanceItemResponse> Post(EnhanceItemRequest request)
    {
        var response = new EnhanceItemResponse();
        response.Result = ErrorCode.None;

        (var errorCode, response.userItem) = await _gameDb.EnhanceItemAsync(request.UserId, request.ItemId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        return response;
    }
}