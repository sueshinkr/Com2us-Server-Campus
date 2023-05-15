using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.ReqRes;

public class KillEnemyRequest : UserAuthRequest
{
    public Int64 UserId { get; set; }
    public Int64 EnemyCode { get; set; }
}

public class KillEnemyResponse
{
    public ErrorCode Result { get; set; } = ErrorCode.None;
}