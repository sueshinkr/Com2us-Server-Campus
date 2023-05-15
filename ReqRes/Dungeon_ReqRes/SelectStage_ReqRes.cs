using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.ReqRes;

public class SelectStageRequest : UserAuthRequest
{
    public Int64 UserId { get; set; }
    public Int64 StageCode { get; set; }
}

public class SelectStageResponse
{
    public ErrorCode Result { get; set; } = ErrorCode.None;
    public List<StageItem> stageItem { get; set; }
    public List<StageEnemy> stageEnemy { get; set; }
}