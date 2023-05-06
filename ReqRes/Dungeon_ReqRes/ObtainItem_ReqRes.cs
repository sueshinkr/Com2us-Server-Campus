using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.RequestResponse;

public class ObtainItemRequest
{
    public Int64 AccountId { get; set; }
    public string AuthToken { get; set; }
    public double AppVersion { get; set; }
    public double MasterVersion { get; set; }
    public Int64 UserId { get; set; }
    public Int64 StageCode { get; set; }
    public Int64 ItemCode { get; set; }
    public Int64 ItemCount { get; set; }
}

public class ObtainItemResponse
{
    public ErrorCode Result { get; set; }
}