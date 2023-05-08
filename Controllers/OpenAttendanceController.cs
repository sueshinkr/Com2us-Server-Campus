using System.ComponentModel.DataAnnotations;
using WebAPIServer.DbOperations;
using WebAPIServer.RequestResponse;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ZLogger;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class OpenAttendance : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;
    readonly IMasterDb _masterDb;

    public OpenAttendance(ILogger<Login> logger, IGameDb gameDb, IMasterDb masterDb)
    {
        _logger = logger;
        _gameDb = gameDb;
        _masterDb = masterDb;
    }

    [HttpPost]
    public async Task<OpenAttendanceResponse> Post(OpenAttendanceRequest request)
    {
        var response = new OpenAttendanceResponse();
        response.Result = ErrorCode.None;

        (var errorCode, response.attendanceCount, response.IsNewAttendance) = await _gameDb.LoadAttendanceDataAsync(request.UserId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        response.attendanceReward = _masterDb.AttendanceRewardInfo;

        return response;
    }
}