using System.ComponentModel.DataAnnotations;
using WebAPIServer.Services;
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
    public async Task<PkCreateAccountResponse> Post(PkCreateAccountRequest request)
    {
        var response = new PkCreateAccountResponse();
        response.Result = ErrorCode.None;

        // 계정 정보 추가
        // Account 테이블에 유저 추가 
        var (errorCode, accountid) = await _accountDb.CreateAccountAsync(request.Email, request.Password);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        // 기본 데이터 생성작업
        // UserData 테이블 / UserItem 테이블에 유저 추가
        errorCode = await _gameDb.CreateBasicData(accountid);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        _logger.ZLogInformation($"{request.Email} Account Created");

        return response;
    }
}

public class PkCreateAccountRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "EMAIL CANNOT BE EMPTY")]
    [StringLength(50, ErrorMessage = "EMAIL IS TOO LONG")]
    [RegularExpression("^[a-zA-Z0-9_\\.-]+@([a-zA-Z0-9-]+\\.)+[a-zA-Z]{2,6}$", ErrorMessage = "E-mail is not valid")]
    public String Email { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "PAS   SWORD CANNOT BE EMPTY")]
    [StringLength(30, ErrorMessage = "PASSWORD IS TOO LONG")]
    [DataType(DataType.Password)]
    public String Password { get; set; }
}

public class PkCreateAccountResponse
{
    public ErrorCode Result { get; set; }
}
