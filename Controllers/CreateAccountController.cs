using System.ComponentModel.DataAnnotations;
using WebAPIServer.Services;
using WebAPIServer.RequestResponse;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ZLogger;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class CreateAccount: ControllerBase
{
    readonly ILogger<CreateAccount> _logger;
    readonly IAccountDb _accountDb;
    readonly IMasterDb _masterDb;
    readonly IGameDb _gameDb;

    public CreateAccount(ILogger<CreateAccount> logger, IAccountDb AccountDb, IMasterDb MasterDb, IGameDb GameDb)
    {
        _logger = logger;
        _accountDb = AccountDb;
        _masterDb = MasterDb;
        _gameDb = GameDb;
    }

    [HttpPost]
    public async Task<CreateAccountResponse> Post(CreateAccountRequest request)
    {
        var response = new CreateAccountResponse();
        response.Result = ErrorCode.None;

        // 계정 정보 추가
        // Account 테이블에 유저 추가 
        (var errorCode, response.AccountId) = await _accountDb.CreateAccountAsync(request.Email, request.Password);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            response.AccountId = 0;
            return response;
        }

        // 기본 데이터 생성작업
        // UserData 테이블 / UserItem 테이블에 유저 추가
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