using System.ComponentModel.DataAnnotations;
using WebAPIServer.DbOperations;
using WebAPIServer.RequestResponse;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ZLogger;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class ReceiveAttendanceReward : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;
    readonly IMasterDb _masterDb;

    public ReceiveAttendanceReward(ILogger<Login> logger, IGameDb gameDb, IMasterDb masterDb)
    {
        _logger = logger;
        _gameDb = gameDb;
        _masterDb = masterDb;
    }

    [HttpPost]
    public async Task<ReceiveAttendanceRewardResponse> Post(ReceiveAttendanceRewardRequest request)
    {
        var response = new ReceiveAttendanceRewardResponse();
        response.Result = ErrorCode.None;

        var errorCode = await _gameDb.SendMailAttendanceRewardAsync(request.UserId, request.AttendanceCount);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }        

        return response;
    }
}