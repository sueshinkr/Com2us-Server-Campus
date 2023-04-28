using WebAPIServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Extensions;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using ZLogger;
using WebAPIServer.ModelDB;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class Login : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IAccountDb _accountDb;
    readonly IRedisDb _redisDb;
    readonly IGameDb _gameDb;

    public Login(ILogger<Login> logger, IAccountDb accountDb, IRedisDb redisDb, IGameDb gameDb)
    {
        _logger = logger;
        _accountDb = accountDb;
        _redisDb = redisDb;
        _gameDb = gameDb;
    }

    [HttpPost]
    public async Task<PkLoginResponse> Post(PkLoginRequest request)
    {
        var response = new PkLoginResponse();
        response.Result = ErrorCode.None;

        // 로그인 정보 검증
        var (errorCode, accountId) = await _accountDb.VerifyAccountAsync(request.Email, request.Password);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        // 게임 데이터 검증
        errorCode = await _redisDb.VerifyGameDataAsync(request.AppVersion, request.MasterVersion);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        // 인증키 생성
        var authToken = Security.RandomString(25);
        errorCode = await _redisDb.RegistUserAsync(request.Email, authToken, accountId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }
        response.Authtoken = authToken;

        // UserData 테이블 / UserItem 테이블에서 유저 정보 찾아서 클라이언트에 전달
        // 기본 데이터 로딩
        (errorCode, response.userData) = await _gameDb.UserDataLoading(accountId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        // 아이템 로딩
        (errorCode, response.userItem) = await _gameDb.UserItemLoading(accountId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        // 공지 읽어오기
        (errorCode, response.notification) = await _redisDb.NotificationLoading();
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
            // 리턴하는게 맞나?
        }

        _logger.ZLogInformation($"{request.Email} Login Success");

        return response;
    }
}

public class PkLoginRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "EMAIL CANNOT BE EMPTY")]
    [StringLength(50, ErrorMessage = "EMAIL IS TOO LONG")]
    [RegularExpression("^[a-zA-Z0-9_\\.-]+@([a-zA-Z0-9-]+\\.)+[a-zA-Z]{2,6}$", ErrorMessage = "E-mail is not valid")]
    public String Email { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "PASSWORD CANNOT BE EMPTY")]
    [StringLength(30, ErrorMessage = "PASSWORD IS TOO LONG")]
    [DataType(DataType.Password)]
    public String Password { get; set; }

    public double AppVersion { get; set; }

    public double MasterVersion { get; set; }
}

public class PkLoginResponse
{
    public ErrorCode Result { get; set; }
    public string Authtoken { get; set; }
    public UserData userData { get; set; }
    public UserItem userItem { get; set; }
    public byte[] notification { get; set; }
}