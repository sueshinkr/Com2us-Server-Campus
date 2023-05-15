using System;
using SqlKata.Execution;
using MySqlConnector;
using WebAPIServer.DataClass;
using ZLogger;
using WebAPIServer.Log;

namespace WebAPIServer.DbOperations;

public partial class GameDb : IGameDb
{
    // 던전 스테이지 로딩
    // ClearStage 테이블에서 클리어한 스테이지 정보 가져오기 
    public async Task<Tuple<ErrorCode, List<ClearData>>> LoadStageListAsync(Int64 userId)
    {
        try
        {
            var clearStage = await _queryFactory.Query("ClearStage").Where("UserId", userId)
                                                .GetAsync<ClearData>() as List<ClearData>;

            return new Tuple<ErrorCode, List<ClearData>>(ErrorCode.None, clearStage);
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.LoadLoadStageListFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "LoadStageList Exception");

            return new Tuple<ErrorCode, List<ClearData>>(errorCode, null);
        }
    }

    // 선택한 스테이지 검증
    // ClearStage 테이블에서 이전 스테이지 클리어 여부 확인하고 MasterData에서 스테이지 정보 가져오기
    public async Task<Tuple<ErrorCode, List<StageItem>, List<StageEnemy>>> SelectStageAsync(Int64 userId, Int64 stageCode)
    {
        try
        {
            if (stageCode != 1)
            {
                var hasQualified = await _queryFactory.Query("ClearStage").Where("UserId", userId)
                                                      .Where("StageCode", stageCode - 1).ExistsAsync();

                if (hasQualified == false)
                {
                    return new Tuple<ErrorCode, List<StageItem>, List<StageEnemy>>(ErrorCode.SelectStageFailNotQualified, null, null);
                }
            }

            var stageItem = _masterDb.StageItemInfo.FindAll(i => i.Code == stageCode);
            var stageEnemy = _masterDb.StageEnemyInfo.FindAll(i => i.Code == stageCode);

            return new Tuple<ErrorCode, List<StageItem>, List<StageEnemy>>(ErrorCode.None, stageItem, stageEnemy);
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.SelectStageFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "SelectStage Exception");

            return new Tuple<ErrorCode, List<StageItem>, List<StageEnemy>>(errorCode, null, null);
        }
    }

    // 던전 클리어 처리
    // redis에 저장하고있었던 획득 목록에 따라 User_Item 테이블에 데이터 추가, User_Data 테이블 업데이트
    public async Task<Tuple<ErrorCode, List<ItemInfo>, Int64>> ReceiveStageClearRewardAsync(Int64 userId, Int64 stageCode, List<ItemInfo> itemList)
    {
        var itemInfo = new List<ItemInfo>();
        Int64 obtainExp = 0;

        try
        {
            (var errorCode, itemInfo) = await ReceiveItemReward(userId, itemList);
            if (errorCode != ErrorCode.None)
            {
                return new Tuple<ErrorCode, List<ItemInfo>, Int64>(errorCode, itemInfo, obtainExp);
            }

            (errorCode, obtainExp) = await ReceiveExpReward(userId, stageCode);
            if (errorCode != ErrorCode.None)
            {
                return new Tuple<ErrorCode, List<ItemInfo>, Int64>(errorCode, itemInfo, obtainExp);
            }

            // 병렬작업...?

            return new Tuple<ErrorCode, List<ItemInfo>, Int64>(ErrorCode.None, itemInfo, obtainExp);
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.ReceiveStageClearRewardFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "ReceiveStageClearReward Exception");

            return new Tuple<ErrorCode, List<ItemInfo>, Int64>(errorCode, itemInfo, obtainExp);
        }
    }

    // 아이템 획득 처리
    // User_Item 테이블에 데이터 추가
    private async Task<Tuple<ErrorCode, List<ItemInfo>>> ReceiveItemReward(Int64 userId, List<ItemInfo> itemList)
    {
        var itemInfo = new List<ItemInfo>();

        try
        {
            foreach (ItemInfo item in itemList)
            {
                var itemType = _masterDb.ItemInfo.Find(i => i.Code == item.ItemCode).Attribute;
                var errorCode = new ErrorCode();

                if (itemType == 4 || itemType == 5) // 마법도구 또는 돈
                {
                    var itemId = _idGenerator.CreateId();

                    (errorCode, var newItem) = await InsertUserItemAsync(userId, item.ItemCode, item.ItemCount, itemId);
                    itemInfo.Add(newItem);
                }
                else
                {
                    for (Int64 i = 0; i < item.ItemCount; i++)
                    {
                        var itemId = _idGenerator.CreateId();

                        (errorCode, var newItem) = await InsertUserItemAsync(userId, item.ItemCode, 1, itemId);
                        if (errorCode != ErrorCode.None)
                        {
                            break;
                        }

                        itemInfo.Add(newItem);
                    }
                }

                if (errorCode != ErrorCode.None)
                {
                    // 롤백
                    for (int i = 0; i <= itemInfo.Count; i++)
                    {
                        await DeleteUserItemAsync(userId, itemInfo[i].ItemId, itemInfo[i].ItemCount);
                    }

                    return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.ReceiveItemRewardFailInsertItem, null);
                }
            }

            return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.None, itemInfo);
        }
        catch (Exception ex)
        {
            // 롤백
            for (int i = 0; i <= itemInfo.Count; i++)
            {
                await DeleteUserItemAsync(userId, itemInfo[i].ItemId, itemInfo[i].ItemCount);
            }

            var errorCode = ErrorCode.ReceiveItemRewardFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "ReceiveItemReward Exception");

            return new Tuple<ErrorCode, List<ItemInfo>>(errorCode, null);
        }
    }

    // 경험치 획득 처리
    // User_Data 테이블 업데이트
    private async Task<Tuple<ErrorCode, Int64>> ReceiveExpReward(Int64 userId, Int64 stageCode)
    {
        Int64 obtainExp = 0;
        var userData = new UserData();

        try
        {
            foreach (StageEnemy stageEnemy in _masterDb.StageEnemyInfo.FindAll(i => i.Code == stageCode))
            {
                obtainExp += stageEnemy.Exp * stageEnemy.Count;
            }

            userData = _queryFactory.Query("User_Data").Where("UserId", userId)
                                    .FirstOrDefault<UserData>();

            var currentExp = userData.Exp + obtainExp;
            var currentLevel = userData.Level;

            while (true)
            {
                var requireExp = _masterDb.ExpTableInfo.Find(i => i.Level == currentLevel).RequireExp;

                if (currentExp >= requireExp)
                {
                    currentExp -= requireExp;

                    currentLevel++;
                }
                else
                {
                    break;
                }
            }

            await _queryFactory.Query("User_Data").Where("UserId", userId)
                               .UpdateAsync(new
                               {
                                   Level = currentLevel,
                                   Exp = currentExp
                               });

            return new Tuple<ErrorCode, Int64>(ErrorCode.None, obtainExp);
        }
        catch (Exception ex)
        {
            //롤백
            await _queryFactory.Query("User_Data").Where("UserId", userId)
                               .UpdateAsync(new
                               {
                                   Level = userData.Level,
                                   Exp = userData.Exp
                               });

            var errorCode = ErrorCode.ReceiveExpRewardFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "ReceiveExpReward Exception");

            return new Tuple<ErrorCode, Int64>(errorCode, 0);
        }
    }
    
    // 클리어 정보 처리
    // ClearStage 테이블에 데이터 추가 또는 업데이트 
    public async Task<ErrorCode> UpdateStageClearDataAsync(Int64 userId, Int64 stageCode, Int64 clearRank, TimeSpan clearTime)
    {
        var beforeClearData = new ClearData();

        try
        {
            beforeClearData = await _queryFactory.Query("ClearStage").Where("UserId", userId)
                                                 .Where("StageCode", stageCode).FirstOrDefaultAsync<ClearData>();

            if (beforeClearData == null)
            {
                await _queryFactory.Query("ClearStage").InsertAsync(new
                {
                    UserId = userId,
                    StageCode = stageCode,
                    ClearRank = clearRank,
                    ClearTime = clearTime
                });
            }
            else if (beforeClearData.ClearRank < clearRank)
            {
                await _queryFactory.Query("ClearStage").Where("UserId", userId)
                                   .Where("StageCode", stageCode).UpdateAsync(new
                                   {
                                       ClearRank = clearRank,
                                       ClearTime = clearTime
                                   });
            }
            else if (beforeClearData.ClearRank == clearRank && beforeClearData.ClearTime > clearTime)
            {
                await _queryFactory.Query("ClearStage").Where("UserId", userId)
                                   .Where("StageCode", stageCode).UpdateAsync(new { ClearTime = clearTime });
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            // 롤백
            if (beforeClearData == null)
            {
                await _queryFactory.Query("ClearStage").Where("UserId", userId)
                                   .Where("StageCode", stageCode).DeleteAsync();
            }
            else
            {
                await _queryFactory.Query("ClearStage").Where("UserId", userId)
                                   .Where("StageCode", stageCode).UpdateAsync(new
                                   {
                                       ClearRank = beforeClearData.ClearRank,
                                       ClearTime = beforeClearData.ClearTime
                                   });
            }

            var errorCode = ErrorCode.UpdateStageClearDataFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "UpdateStageClearData Exception");
            
            return errorCode;
        }
    }
}
