﻿using System.ComponentModel.DataAnnotations;
using WebAPIServer.DbOperations;
using WebAPIServer.RequestResponse;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ZLogger;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class OpenAttendanceSheet : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IGameDb _gameDb;
    readonly IMasterDb _masterDb;

    public OpenAttendanceSheet(ILogger<Login> logger, IGameDb gameDb, IMasterDb masterDb)
    {
        _logger = logger;
        _gameDb = gameDb;
        _masterDb = masterDb;
    }

    [HttpPost]
    public async Task<OpenAttendanceSheetResponse> Post(OpenAttendanceSheetRequest request)
    {
        var response = new OpenAttendanceSheetResponse();
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