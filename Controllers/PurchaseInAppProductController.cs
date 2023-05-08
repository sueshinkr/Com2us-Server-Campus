﻿using System.ComponentModel.DataAnnotations;
using WebAPIServer.DbOperations;
using WebAPIServer.RequestResponse;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ZLogger;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class PurchaseInAppProduct : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;

    public PurchaseInAppProduct(ILogger<Login> logger, IGameDb gameDb)
    {
        _logger = logger;
        _gameDb = gameDb;
    }

    [HttpPost]
    public async Task<PurchaseInAppProductResponse> Post(PurchaseInAppProductRequest request)
    {
        var response = new PurchaseInAppProductResponse();
        response.Result = ErrorCode.None;

        var errorCode = await _gameDb.PurchaseInAppProductAsync(request.UserId, request.PurchaseId, request.ProductCode);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        return response;
    }
}