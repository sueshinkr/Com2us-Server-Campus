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
public class ReadMail : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;

    public ReadMail(ILogger<Login> logger, IGameDb gameDb)
    {
        _logger = logger;
        _gameDb = gameDb;
    }

    [HttpPost]
    public async Task<ReadMailResponse> Post(ReadMailRequest request)
    {
        var response = new ReadMailResponse();
        response.Result = ErrorCode.None;

        (var errorCode, response.Content, response.Item) = await _gameDb.MailReadingAsync(request.MailId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        return response;
    }
}
