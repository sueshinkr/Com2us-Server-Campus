using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.RequestResponse;

public class ReadMailRequest
{
    public Int64 AccountId { get; set; }
    public string AuthToken { get; set; }
    public double AppVersion { get; set; }
    public double MasterVersion { get; set; }
    public Int64 MailId { get; set; }
    public Int64 UserId { get; set; }
}

public class ReadMailResponse
{
    public ErrorCode Result { get; set; }
    public string Content { get; set; }
    public List<MailItem> Item { get; set; }
}