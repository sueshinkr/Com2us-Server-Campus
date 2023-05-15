using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.ReqRes;

public class ObtainItemRequest : UserAuthRequest
{
    public Int64 UserId { get; set; }
    public Int64 ItemCode { get; set; }
    public Int64 ItemCount { get; set; }
}

public class ObtainItemResponse
{
    public ErrorCode Result { get; set; } = ErrorCode.None;
}