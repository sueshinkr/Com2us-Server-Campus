﻿using System;
using WebAPIServer.DataClass;

namespace WebAPIServer.Services;

public interface IMasterDb
{
    VersionData VersionDataInfo { get; }
    List<Item> ItemInfo { get; }
    List<ItemAttribute> ItemAttributeInfo { get; }
    List<AttendanceReward> AttendanceRewardInfo { get; }
    List<InAppProduct> InAppProductInfo { get; }
    List<StageItem> StageItemInfo { get; }
    List<StageEnemy> StageEnemyInfo { get; }

    public ErrorCode VerifyVersionDataAsync(double appVersion, double masterVersion);
}

