namespace WebAPIServer.DataClass;

public class ObtainedStageItem
{
    public ObtainedStageItem(Int64 itemCode, Int64 itemCount)
    {
        ItemCode = itemCode;
        ItemCount = itemCount;
    }

    public Int64 ItemCode { get; set; }
    public Int64 ItemCount { get; set; }
}

public class KilledStageEnemy
{
    private int v;

    public KilledStageEnemy(long enemyCode, Int64 enemyCount)
    {
        EnemyCode = enemyCode;
        EnemyCount = enemyCount;
    }

    public Int64 EnemyCode { get; set; }
    public Int64 EnemyCount { get; set; }
}