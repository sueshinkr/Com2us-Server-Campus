using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.RequestResponse;

public class ClearStageRequest
{
    public Int64 AccountId { get; set; }
    public string AuthToken { get; set; }
    public double AppVersion { get; set; }
    public double MasterVersion { get; set; }
    public Int64 UserId { get; set; }
    public Int64 ClearRank { get; set; }
    public TimeSpan ClearTime { get; set; }
}

public class ClearStageResponse
{
    public ErrorCode Result { get; set; }
    public List<ItemInfo> itemInfo { get; set; }
    public Int64 ObtainExp { get; set; }
}