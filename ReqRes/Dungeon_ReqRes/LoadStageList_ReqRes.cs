using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.RequestResponse;

public class LoadStageListRequest
{
    public Int64 AccountId { get; set; }
    public string AuthToken { get; set; }
    public double AppVersion { get; set; }
    public double MasterVersion { get; set; }
    public Int64 UserId { get; set; }
}

public class LoadStageListResponse
{
    public ErrorCode Result { get; set; }
    public List<ClearData> ClearStage { get; set; }
}