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
public class ReceiveItemFromMail : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;

    public ReceiveItemFromMail(ILogger<Login> logger, IGameDb gameDb)
    {
        _logger = logger;
        _gameDb = gameDb;
    }

    [HttpPost]
    public async Task<ReceiveItemFromMailResponse> Post(ReceiveItemFromMailRequest request)
    {
        var response = new ReceiveItemFromMailResponse();
        response.Result = ErrorCode.None;

        (var errorCode, response.Item) = await _gameDb.MailItemReceivingAsync(request.ItemId, request.UserId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        return response;
    }
}
