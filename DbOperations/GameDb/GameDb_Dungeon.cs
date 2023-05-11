using System;
using SqlKata.Execution;
using MySqlConnector;
using WebAPIServer.DataClass;
using ZLogger;

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
            _logger.ZLogError(ex, $"[LoadStageList] ErrorCode: {ErrorCode.LoadLoadStageListFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, List<ClearData>>(ErrorCode.LoadLoadStageListFailException, null);
        }
    }

    // 선택한 스테이지 검증
    // ClearStage 테이블에서 이전 스테이지 클리어 여부 확인하고 MasterData에서 스테이지 정보 가져오기
    public async Task<Tuple<ErrorCode, List<Int64>, List<StageEnemy>>> SelectStageAsync(Int64 userId, Int64 stageCode)
    {
        try
        {
            if (stageCode != 1)
            {
                var hasQualified = await _queryFactory.Query("ClearStage").Where("UserId", userId)
                                                .Where("StageCode", stageCode - 1).ExistsAsync();

                if (hasQualified == false)
                {
                    _logger.ZLogError($"[SelectStage] ErrorCode: {ErrorCode.SelectStageFailNotQualified}, UserId: {userId}, StageNum : {stageCode}");
                    return new Tuple<ErrorCode, List<Int64>, List<StageEnemy>>(ErrorCode.SelectStageFailNotQualified, null, null);
                }
            }

            var stageItem = _masterDb.StageItemInfo.FindAll(i => i.Code == stageCode).Select(i => i.ItemCode).ToList();
            var stageEnemy = _masterDb.StageEnemyInfo.FindAll(i => i.Code == stageCode);

            return new Tuple<ErrorCode, List<Int64>, List<StageEnemy>>(ErrorCode.None, stageItem, stageEnemy);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[SelectStage] ErrorCode: {ErrorCode.SelectStageFailException}, UserId: {userId}, StageNum : {stageCode}");
            return new Tuple<ErrorCode, List<Int64>, List<StageEnemy>>(ErrorCode.SelectStageFailException, null, null);
        }
    }

    // 던전 클리어 처리
    // redis에 저장하고있었던 획득 목록에 따라 User_Item 테이블에 데이터 추가, User_Data 테이블 업데이트
    public async Task<Tuple<ErrorCode, List<UserItem>, Int64>> ReceiveStageClearRewardAsync(Int64 userId, Int64 stageCode, List<ObtainedStageItem> itemList)
    {
        var userItem = new List<UserItem>();
        Int64 obtainExp = 0;

        try
        {
            (var errorCode, userItem) = await ReceiveItemReward(userId, itemList);
            if (errorCode != ErrorCode.None)
            {
                return new Tuple<ErrorCode, List<UserItem>, Int64>(errorCode, userItem, obtainExp);
            }

            (errorCode, obtainExp) = await ReceiveExpReward(userId, stageCode);
            if (errorCode != ErrorCode.None)
            {
                return new Tuple<ErrorCode, List<UserItem>, Int64>(errorCode, userItem, obtainExp);
            }

            return new Tuple<ErrorCode, List<UserItem>, Int64>(ErrorCode.None, userItem, obtainExp);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[ReceiveStageClearReward] ErrorCode: {ErrorCode.ReceiveStageClearRewardFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, List<UserItem>, Int64>(ErrorCode.ReceiveStageClearRewardFailException, userItem, obtainExp);
        }
    }

    // 아이템 획득 처리
    // User_Item 테이블에 데이터 추가
    private async Task<Tuple<ErrorCode, List<UserItem>>> ReceiveItemReward(Int64 userId, List<ObtainedStageItem> itemList)
    {
        var userItem = new List<UserItem>();

        try
        {
            foreach (ObtainedStageItem item in itemList)
            {
                var itemType = _masterDb.ItemInfo.Find(i => i.Code == item.ItemCode).Attribute;
                var errorCode = new ErrorCode();

                if (itemType == 4 || itemType == 5) // 마법도구 또는 돈
                {
                    var itemId = _idGenerator.CreateId();

                    (errorCode, var newItem) = await InsertUserItemAsync(userId, item.ItemCode, item.ItemCount, itemId);
                    userItem.Add(newItem);
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

                        userItem.Add(newItem);
                    }
                }

                if (errorCode != ErrorCode.None)
                {
                    // 롤백
                    for (int i = 0; i <= userItem.Count; i++)
                    {
                        await DeleteUserItemAsync(userId, userItem[i].ItemId, userItem[i].ItemCount);
                    }

                    _logger.ZLogError($"[ReceiveItemReward] ErrorCode: {ErrorCode.ReceiveItemRewardFailInsertItem}, UserId: {userId}");
                    return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.ReceiveItemRewardFailInsertItem, new List<UserItem>());
                }
            }

            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.None, userItem);
        }
        catch (Exception ex)
        {
            // 롤백
            for (int i = 0; i <= userItem.Count; i++)
            {
                await DeleteUserItemAsync(userId, userItem[i].ItemId, userItem[i].ItemCount);
            }

            _logger.ZLogError(ex, $"[ReceiveItemReward] ErrorCode: {ErrorCode.ReceiveItemRewardFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.ReceiveItemRewardFailException, new List<UserItem>());
        }
    }

    // 경험치 획득 처리
    // User_Data 테이블 업데이트
    private async Task<Tuple<ErrorCode, Int64>> ReceiveExpReward(Int64 userId, Int64 stageCode)
    {
        Int64 totalObtainExp = 0;
        var userData = new UserData();

        try
        {
            foreach (StageEnemy stageEnemy in _masterDb.StageEnemyInfo.FindAll(i => i.Code == stageCode))
            {
                totalObtainExp += stageEnemy.Exp * stageEnemy.Count;
            }

            userData = _queryFactory.Query("User_Data").Where("UserId", userId)
                                        .FirstOrDefault<UserData>();
            var obtainExp = totalObtainExp;
            var currentExp = userData.Exp;
            var currentLevel = userData.Level;

            while (true)
            {
                var requireExp = _masterDb.ExpTableInfo.Find(i => i.Level == currentLevel).RequireExp;

                if (currentExp + obtainExp >= requireExp)
                {
                    var tmpExp = currentExp;
                    currentExp = currentExp + obtainExp - requireExp;
                    obtainExp -= requireExp - tmpExp;

                    currentLevel++;
                }
                else
                {
                    currentExp += obtainExp;
                    break;
                }
            }

            await _queryFactory.Query("User_Data").Where("UserId", userId)
                               .UpdateAsync(new
                               {
                                   Level = currentLevel,
                                   Exp = currentExp
                               });

            return new Tuple<ErrorCode, Int64>(ErrorCode.None, totalObtainExp);
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

            _logger.ZLogError(ex, $"[ReceiveExpReward] ErrorCode: {ErrorCode.ReceiveExpRewardFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, Int64>(ErrorCode.ReceiveExpRewardFailException, 0);
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

            _logger.ZLogError(ex, $"[UpdateStageClearData] ErrorCode: {ErrorCode.UpdateStageClearDataFailException}, UserId: {userId}");
            return ErrorCode.UpdateStageClearDataFailException;
        }
    }

    
}
