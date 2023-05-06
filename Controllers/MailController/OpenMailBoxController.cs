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
public class OpenMailBox : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;

    public OpenMailBox(ILogger<Login> logger, IGameDb gameDb)
    {
        _logger = logger;
        _gameDb = gameDb;
    }

    [HttpPost]
    public async Task<OpenMailBoxResponse> Post(OpenMailBoxRequest request)
    {
        var response = new OpenMailBoxResponse();
        response.Result = ErrorCode.None;

        (var errorCode, response.mailData) = await _gameDb.MailDataLoadingAsync(request.UserId, request.PageNumber);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        return response;
    }
}
