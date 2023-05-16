namespace WebAPIServer.DataClass;

public class KilledStageEnemy
{
    public Int64 GoalCount { get; set; }
    public Int64 KilledCount { get; set; }
}

public class ObtainedStageItem
{
    public Int64 MaxCount { get; set; }
    public Int64 ObtainedCount { get; set; }
}

public class RediskeyExpireTime
{
    public const ushort NxKeyExpireSecond = 3;
    public const ushort LoginKeyExpireMin = 600;
    public const ushort DungeonKeyExpireMin = 60;
}