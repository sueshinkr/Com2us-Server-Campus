using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.ReqRes;

public class ReceiveChatRequest
{
    public Int64 AccountId { get; set; }
    public string AuthToken { get; set; }
    public double AppVersion { get; set; }
    public double MasterVersion { get; set; }
    public Int64 UserId { get; set; }
}

public class ReceiveChatResponse
{
    public ErrorCode Result { get; set; } = ErrorCode.None;
    public List<string> ChatHistory { get; set; }
}