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
    // redis에 저장하고있었던 획득 목록에 따라 User_Item 테이블에 데이터 추가, ClearStage 테이블에 데이터 추가, User_Data 테이블 업데이트 
    public async Task<ErrorCode> GetStageClearRewardAsync(Int64 userId, Int64 stageCode, Int64 clearRank, TimeSpan clearTime, List<ObtainedStageItem> itemList)
    {
        var itemIdList = new List<Int64>();

        try
        {
            // 아이템 획득 처리
            foreach (ObtainedStageItem item in itemList)
            {
                var itemType = _masterDb.ItemInfo.Find(i => i.Code == item.ItemCode).Attribute;
                var errorCode = new ErrorCode();

                if (itemType == 4 || itemType == 5) // 마법도구 또는 돈
                {
                    var itemId = _idGenerator.CreateId();
                    itemIdList.Add(itemId);

                    errorCode = await InsertUserItemAsync(userId, item.ItemCode, item.ItemCount, itemId);
                }
                else
                {
                    for (Int64 i = 0; i < item.ItemCount; i++)
                    {
                        var itemId = _idGenerator.CreateId();
                        itemIdList.Add(itemId);

                        errorCode = await InsertUserItemAsync(userId, item.ItemCode, 1, itemId);
                    }
                }

                if (errorCode != ErrorCode.None)
                {
                    // 롤백
                    for (int i = 0; i <= itemIdList.Count; i++)
                    {
                        await DeleteUserItemAsync(userId, itemIdList[i]);
                    }

                    _logger.ZLogError($"[GetStageClearReward] ErrorCode: {ErrorCode.GetStageClearRewardFailInsertItem}, UserId: {userId}");
                    return ErrorCode.GetStageClearRewardFailInsertItem;
                }
            }

            // 클리어 정보 처리
            var beforeClearData = await _queryFactory.Query("ClearStage").Where("UserId", userId)
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

            // 경험치 처리
            // 이런거 함수로 따로 빼도 될듯?
            Int64 obtainExp = 0;
            foreach (StageEnemy stageEnemy in _masterDb.StageEnemyInfo.FindAll(i => i.Code == stageCode))
            {
                obtainExp += stageEnemy.Exp * stageEnemy.Count;
            }

            var userData = _queryFactory.Query("User_Data").Where("UserId", userId)
                                        .FirstOrDefault<UserData>();

            while (true)
            {
                var requireExp = _masterDb.ExpTableInfo.Find(i => i.Level == userData.Level).RequireExp;

                if (userData.Exp + obtainExp >= requireExp)
                {
                    var tmpExp = userData.Exp;
                    userData.Exp = userData.Exp + obtainExp - requireExp;
                    obtainExp -= requireExp - tmpExp;

                    userData.Level++;
                }
                else
                {
                    userData.Exp += obtainExp;
                    break;
                }
            }

            await _queryFactory.Query("User_Data").Where("UserId", userId)
                               .UpdateAsync(new
                               {
                                   Level = userData.Level,
                                   Exp = userData.Exp
                               });
            
            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[GetStageClearReward] ErrorCode: {ErrorCode.GetStageClearRewardFailException}, UserId: {userId}");
            return ErrorCode.GetStageClearRewardFailException;
        }
    }
}
