namespace WebAPIServer.ModelDB;

public class UserData
{
    public Int64 AccountId { get; set; }
    public Int64 Level { get; set; }
    public Int64 Exp { get; set; }
    public Int64 Money { get; set; }
    public Int64 AttendanceCount { get; set; }
    public Int64 ClearStage { get; set; }
}

public class UserItem
{
    public List<UserItem_Consumable> Consumable { get; set; }
    public List<UserItem_Equipment> Equipment { get; set; }
}

public class UserItem_Consumable
{
    public Int64 AccountId { get; set; }
    public Int64 ItemCode { get; set; }
    public Int64 ItemCount { get; set; }
}

public class UserItem_Equipment
{
    public Int64 UniqueId { get; set; }
    public Int64 AccountId { get; set; }
    public Int64 ItemCode { get; set; }
    public DateTime ObtainDate { get; set; }
    public Int64 EnhanceCount { get; set; }
}