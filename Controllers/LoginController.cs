using WebAPIServer.DbOperations;
using WebAPIServer.RequestResponse;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Extensions;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using ZLogger;
using WebAPIServer.DataClass;

namespace WebAPIServer.Controllers;

[ApiController]
[Route("[controller]")]
public class Login : ControllerBase
{
    readonly ILogger<Login> _logger;
    readonly IAccountDb _accountDb;
    readonly IRedisDb _redisDb;
    readonly IGameDb _gameDb;
    readonly IMasterDb _masterDb;

    public Login(ILogger<Login> logger, IAccountDb accountDb, IRedisDb redisDb, IGameDb gameDb, IMasterDb masterDb)
    {
        _logger = logger;
        _accountDb = accountDb;
        _redisDb = redisDb;
        _gameDb = gameDb;
        _masterDb = masterDb;
    }

    [HttpPost]
    public async Task<LoginResponse> Post(LoginRequest request)
    {
        var response = new LoginResponse();
        response.Result = ErrorCode.None;

        // 로그인 정보 검증
        var (errorCode, accountId) = await _accountDb.VerifyAccountAsync(request.Email, request.Password);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        // 버전 데이터 검증
        errorCode = _masterDb.VerifyVersionDataAsync(request.AppVersion, request.MasterVersion);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        // 인증키 생성
        var authToken = Security.RandomString(25);
        errorCode = await _redisDb.CreateUserDataAsync(request.Email, authToken, accountId);
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

        var userid = response.userData.UserId;

        // 아이템 로딩
        (errorCode, response.userItem) = await _gameDb.UserItemLoadingAsync(userid);
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
        }

        return response;
    }
}