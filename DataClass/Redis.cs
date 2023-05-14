namespace WebAPIServer.DataClass;

public class KilledEnemy
{
    public Int64 EnemyCode { get; set; }
    public Int64 EnemyCount { get; set; }
}

public class KilledStageEnemy
{
    public List<KilledEnemy> KilledEnemyList { get; set; }
    public Int64 StageCode { get; set; }
}

public class ObtainedStageItem
{
    public List<ItemInfo> obtainedItemList { get; set; }
    public Int64 StageCode { get; set; }
}