using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebAPIServer.RequestResponse;
using WebAPIServer.Services;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class Mail : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IAccountDb _accountDb;
    readonly IRedisDb _redisDb;
    readonly IGameDb _gameDb;

    public Mail(ILogger<Login> logger, IAccountDb accountDb, IRedisDb redisDb, IGameDb gameDb)
    {
        _logger = logger;
        _accountDb = accountDb;
        _redisDb = redisDb;
        _gameDb = gameDb;
    }

    [HttpPost]
    public async Task<MailResponse> Post(MailRequest request)
    {
        var response = new MailResponse();
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
