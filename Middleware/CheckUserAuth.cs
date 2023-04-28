using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using WebAPIServer.ModelDB;
using WebAPIServer.Services;
using ZLogger;

namespace WebAPIServer.Middleware;

// You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
public class CheckUserAuth
{
    private readonly RequestDelegate _next;
    readonly ILogger<CheckUserAuth> _logger;
    private readonly IRedisDb _redisDb;
    private readonly IGameDb _gameDb;


    public CheckUserAuth(RequestDelegate next, ILogger<CheckUserAuth> logger, IRedisDb redisDb, IGameDb gameDb)
    {
        _next = next;
        _logger = logger;
        _redisDb = redisDb;
        _gameDb = gameDb;
    }

    public async Task Invoke(HttpContext context)
    {
        var formString = context.Request.Path.Value;
        if (string.Compare(formString, "/CreateAccount", StringComparison.OrdinalIgnoreCase) == 0 ||
            string.Compare(formString, "/Login", StringComparison.OrdinalIgnoreCase) == 0)
        {
            await _next(context);

            return;
        }

        context.Request.EnableBuffering();

        string accountId;
        string authToken;
        double appVersion;
        double masterVersion;
        string userLockKey = "";

        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 4096, true))
        {
            var bodyStr = await reader.ReadToEndAsync();

            // request 데이터 유무 확인
            if (IsNullBodyData(context, bodyStr))
            {
                await SetJsonResponse(context, ErrorCode.EmptyRequestHttpBody);
                return;
            }

            // request의 필요 데이터 존재 여부 확인
            var document = JsonDocument.Parse(bodyStr);
            if (IsInvalidJsonFormat(context, document, out accountId, out authToken, out appVersion, out masterVersion))
            {
                await SetJsonResponse(context, ErrorCode.InvalidRequestHttpBody);
                return;
            }

            // 게임 데이터 확인
            if (await _redisDb.VerifyGameDataAsync(appVersion, masterVersion) != ErrorCode.None)
            {
                await SetJsonResponse(context, ErrorCode.CheckUserGameDataNotMatch);
                return;
            }

            // User Regist 여부 확인
            var userInfo = await _redisDb.GetUserAsync(accountId);
            if (userInfo == null)
            {
                await SetJsonResponse(context, ErrorCode.UserNotRegisted);
                return;
            }

            // AuthToken 확인
            if (IsInvalidUserAuthToken(context, userInfo, authToken))
            {
                await SetJsonResponse(context, ErrorCode.AuthTokenFailWrongAuthToken);
                return;
            }

            // Lock
            userLockKey = "ULock_" + accountId;
            if (await _redisDb.SetUserReqLockAsync(userLockKey))
            {
                await SetJsonResponse(context, ErrorCode.AuthTokenFailSetNx);
                return;
            }

            context.Items[nameof(AuthUser)] = userInfo;
        }

        context.Request.Body.Position = 0;

        await _next(context);
        await _redisDb.DelUserReqLockAsync(userLockKey);
    }

    public bool IsNullBodyData(HttpContext context, string bodyStr)
    {
        if (string.IsNullOrEmpty(bodyStr) == false)
        {
            return false;
        }

        return true;
    }

    public bool IsInvalidJsonFormat(HttpContext context, JsonDocument document, out string accountId, out string authToken, out double appVersion, out double masterVersion)
    {
        try
        {
            accountId = document.RootElement.GetProperty("AccountId").GetString();
            authToken = document.RootElement.GetProperty("AuthToken").GetString();
            appVersion = document.RootElement.GetProperty("AppVersion").GetDouble();
            masterVersion = document.RootElement.GetProperty("MasterVersion").GetDouble();
            return false;
        }
        catch
        {
            accountId = ""; authToken = ""; appVersion = 0; masterVersion = 0;
            return true;
        }
    }

    public bool IsInvalidUserAuthToken(HttpContext context, AuthUser userInfo, string authToken)
    {
        if (string.CompareOrdinal(userInfo.AuthToken, authToken) == 0)
        {
            return false;
        }
        return true;
    }

    public async Task<ErrorCode> SetJsonResponse(HttpContext context, ErrorCode errorcode)
    {
        try
        {
            var JsonResponse = JsonSerializer.Serialize(new CheckUserAuthResponse
            {
                result = errorcode
            });

            var bytes = Encoding.UTF8.GetBytes(JsonResponse);
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);

            _logger.ZLogError($"[CheckUserAuth] ErrorCode: {errorcode}");

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[CheckUserAuth] ErrorCode: {ErrorCode.SetJsonFailException}");
            return ErrorCode.SetJsonFailException;
        }
    }
}

public class CheckUserAuthResponse
{
    public ErrorCode result { get; set; }
}