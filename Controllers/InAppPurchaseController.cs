using System.ComponentModel.DataAnnotations;
using WebAPIServer.Services;
using WebAPIServer.RequestResponse;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ZLogger;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class InAppPurchase : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;

    public InAppPurchase(ILogger<Login> logger, IGameDb gameDb)
    {
        _logger = logger;
        _gameDb = gameDb;
    }

    [HttpPost]
    public async Task<InAppPurchaseResponse> Post(InAppPurchaseRequest request)
    {
        var response = new InAppPurchaseResponse();
        response.Result = ErrorCode.None;

        var errorCode = await _gameDb.InAppPurchasingAsync(request.UserId, request.PurchaseId, request.ProductCode);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        return response;
    }
}