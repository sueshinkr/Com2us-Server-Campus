using WebAPIServer.Services;
using WebAPIServer.RequestResponse;
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

        // 게임 데이터 검증
        errorCode = await _redisDb.VerifyVersionDataAsync(request.AppVersion, request.MasterVersion);
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

        var userid = response.userData.UserId;

        // 아이템 로딩
        (errorCode, response.userItem) = await _gameDb.UserItemLoading(userid);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        _logger.ZLogInformation($"{request.Email} Login Success");

        // 공지 읽어오기
        (errorCode, response.notification) = await _redisDb.NotificationLoading();
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
        }

        return response;
    }
}