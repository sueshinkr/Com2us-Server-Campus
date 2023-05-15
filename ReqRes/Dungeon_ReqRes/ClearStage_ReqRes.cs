using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.ReqRes;

public class ClearStageRequest : UserAuthRequest
{
    public Int64 UserId { get; set; }
    public Int64 ClearRank { get; set; }
    public TimeSpan ClearTime { get; set; }
}

public class ClearStageResponse
{
    public ErrorCode Result { get; set; } = ErrorCode.None;
    public List<ItemInfo> itemInfo { get; set; }
    public Int64 ObtainExp { get; set; }
}