using System.ComponentModel.DataAnnotations;
using WebAPIServer.Services;
using WebAPIServer.RequestResponse;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ZLogger;

namespace WebAPIServer.Controllers;
/*
[ApiController]
[Route("[controller]")]
public class OpenAttendance: ControllerBase
{
    readonly ILogger<CreateAccount> _logger;
    readonly
    readonly IMasterDb _masterDb;
    readonly IGameDb _gameDb;

    public OpenAttendance(ILogger<CreateAccount> logger, IMasterDb masterDb, IGameDb gameDb)
    {
        _logger = logger;
        _masterDb = masterDb;
        _gameDb = gameDb;
    }

    [HttpPost]
    public async Task<OpenAttendanceResponse> Post(OpenAttendanceRequest request)
    {
        var response = new CreateAccountResponse();
        response.Result = ErrorCode.None;

        // 계정 정보 생성
        // Account 테이블에 계정 추가 
        (var errorCode, response.AccountId) = await _accountDb.CreateAccountAsync(request.Email, request.Password);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            response.AccountId = 0;
            return response;
        }

        // 유저 기본 데이터 생성
        // User_Data 테이블에 유저 추가 / User_Item 테이블에 아이템 추가
        errorCode = await _gameDb.CreateBasicDataAsync(response.AccountId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            response.AccountId = 0;
            return response;
        }

        _logger.ZLogInformation($"{request.Email} Account Created");

        return response;
    }
}
*/