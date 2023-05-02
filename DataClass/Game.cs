namespace WebAPIServer.DataClass;

public class UserData
{
    public Int64 AccountId { get; set; }
    public Int64 UserId { get; set; }
    public Int64 Level { get; set; }
    public Int64 Exp { get; set; }
    public Int64 Money { get; set; }
    public Int64 AttendanceCount { get; set; }
    public Int64 ClearStage { get; set; }
}

public class UserItem
{
    public Int64 ItemId { get; set; }
    public Int64 UserId { get; set; }
    public Int64 ItemCode { get; set; }
    public Int64 ItemCount { get; set; }
    public Int64 EnhanceCount { get; set; }
    public DateTime ObtainedAt { get; set; }
}

public class MailItem
{
    public Int64 ItemId { get; set; }
    public Int64 ItemCode { get; set; }
    public Int64 ItemCount { get; set; }
    public bool IsReveived { get; set; }
}

public class MailContent
{
    public string Content { get; set; }
}

public class MailData
{
    public Int64 MailId { get; set; }
    public Int64 UserId { get; set; }

    public string SenderName { get; set; }
    public string Title { get; set; }

    public bool IsRead { get; set; }
    public bool HasItem { get; set; }

    public DateTime ObtainedAt { get; set; }
    public DateTime ExpiredAt { get; set; }
}