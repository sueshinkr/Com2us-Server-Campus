using System;
using WebAPIServer.ModelDB;

namespace WebAPIServer.Services;

public interface IMasterDb
{
    GameData GameDataInfo { get; }
    List<Item> ItemInfo { get; }
    List<ItemAttribute> ItemAttributeInfo { get; }
    List<AttendanceReward> AttendanceRewardInfo { get; }
    List<InAppProduct> InAppProductInfo { get; }
    List<StageItem> StageItemInfo { get; }
    List<StageEnemy> StageEnemyInfo { get; }
}

