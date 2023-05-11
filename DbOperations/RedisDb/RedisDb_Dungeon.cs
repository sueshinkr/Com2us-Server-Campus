using System;
using CloudStructures.Structures;
using StackExchange.Redis;
using WebAPIServer.DataClass;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class RedisDb : IRedisDb
{
    // 스테이지 진행 정보 생성
    // UserId로 키 생성
    public async Task<ErrorCode> CreateStageProgressDataAsync(Int64 userId, Int64 stageCode)
    {
        var stageItemKey = "Stage_" + userId + "_Item";
        var stageEnemyKey = "Stage_" + userId + "_Enemy";

        try
        {
            var itemRedis = new RedisString<Tuple<List<ItemInfo>, Int64>>(_redisConn, stageItemKey, null);
            var enemyRedis = new RedisString<Tuple<List<KilledStageEnemy>, Int64>>(_redisConn, stageEnemyKey, null);

            var item = await itemRedis.GetAsync();
            var enemy = await enemyRedis.GetAsync();

            if (item.HasValue == true || enemy.HasValue == true)
            {
                await DeleteStageProgressDataAsync(userId, stageCode);

                return ErrorCode.CreateStageProgressDataFailAlreadyIn;
            }
            else
            {
                var itemList = new Tuple<List<ItemInfo>, Int64>(new List<ItemInfo>(), stageCode);
                var enemyList = new Tuple<List<KilledStageEnemy>, Int64>(new List<KilledStageEnemy>(), stageCode);

                if (await itemRedis.SetAsync(itemList) == false || await enemyRedis.SetAsync(enemyList) == false)
                {
                    return ErrorCode.CreateStageProgressDataFailRedis;
                }

                return ErrorCode.None;
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "CreateStageProgressData Exception");

            return ErrorCode.CreateStageProgressDataFailException;
        }
    }

    // 스테이지 진행 정보 삭제
    // UserId에 해당하는 키 제거
    public async Task<ErrorCode> DeleteStageProgressDataAsync(Int64 userId, Int64 stageCode)
    {
        var stageItemKey = "Stage_" + userId + "_Item";
        var stageEnemyKey = "Stage_" + userId + "_Enemy";

        try
        {
            var itemRedis = new RedisString<Tuple<List<ItemInfo>, Int64>>(_redisConn, stageItemKey, null);
            var enemyRedis = new RedisString<Tuple<List<KilledStageEnemy>, Int64>>(_redisConn, stageEnemyKey, null);

            if (await itemRedis.DeleteAsync() == false | await enemyRedis.DeleteAsync() == false)
            {
                return ErrorCode.DeleteStageProgressDataFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "DeleteStageProgressData Exception");
           
            return ErrorCode.DeleteStageProgressDataFailException;
        }
    }

    // 스테이지 아이템 획득
    // 유저의 stageItemKey에 아이템 추가
    public async Task<ErrorCode> ObtainItemAsync(Int64 userId, Int64 stageCode, Int64 itemCode, Int64 itemCount)
    {
        var isRightItem = _masterDb.StageItemInfo.Find(i => i.Code == stageCode && i.ItemCode == itemCode);

        if (isRightItem == null)
        {
            return ErrorCode.ObtainItemFailWrongItem;
        }

        var stageItemKey = "Stage_" + userId + "_Item";

        try
        {
            var redis = new RedisString<Tuple<List<ItemInfo>, Int64>>(_redisConn, stageItemKey, null);
            var itemResult = await redis.GetAsync();

            if (itemResult.HasValue == false)
            {
                return ErrorCode.ObtainItemFailWrongKey;
            }

            if (itemResult.Value.Item2 != stageCode)
            {                
                return ErrorCode.ObtainItemFailWrongStage;
            }

            var itemlist = itemResult.Value.Item1;
            var index = itemlist.FindIndex(i => i.ItemCode == itemCode);

            if (index >= 0)
            {
                itemlist[index].ItemCount += itemCount;
            }
            else
            {
                var newItem = new ItemInfo();
                newItem.ItemCode = itemCode;
                newItem.ItemCount = itemCount;

                itemlist.Add(newItem);
            }

            if (await redis.SetAsync(new Tuple<List<ItemInfo>, Int64>(itemlist, stageCode)) == false)
            {
                return ErrorCode.ObtainItemFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "ObtainItem Exception");
            
            return ErrorCode.ObtainItemFailException;
        }
    }

    // 스테이지 적 제거 
    // 유저의 stageEnemyKey에 적 추가
    public async Task<ErrorCode> KillEnemyAsync(Int64 userId, Int64 stageCode, Int64 enemyCode)
    {
        var isRightEnemy = _masterDb.StageEnemyInfo.Find(i => i.Code == stageCode && i.NpcCode == enemyCode);

        if (isRightEnemy == null)
        {            
            return ErrorCode.KillEnemyFailWrongEnemy;
        }

        var stageEnemyKey = "Stage_" + userId + "_Enemy";

        try
        {
            var redis = new RedisString<Tuple<List<KilledStageEnemy>, Int64>>(_redisConn, stageEnemyKey, null);
            var enemyResult = await redis.GetAsync();

            if (enemyResult.HasValue == false)
            {
                return ErrorCode.KillEnemyFailWrongKey;
            }

            if (enemyResult.Value.Item2 != stageCode)
            { 
                return ErrorCode.KillEnemyFailWrongStage;
            }

            var enemyList = enemyResult.Value.Item1;           
            var index = enemyList.FindIndex(i => i.EnemyCode == enemyCode);

            if (index >= 0)
            {
                enemyList[index].EnemyCount += 1;
            }
            else
            {
                enemyList.Add(new KilledStageEnemy(enemyCode, 1));
            }

            if (await redis.SetAsync(new Tuple<List<KilledStageEnemy>, Int64>(enemyList, stageCode)) == false)
            {
                return ErrorCode.KillEnemyFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "KillEnemy Exception");

            return ErrorCode.KillEnemyFailException;
        }
    }

    // 스테이지 클리어 확인
    // MasterData의 StageEnemy 데이터와 redis에 저장해놓은 데이터 비교 
    public async Task<Tuple<ErrorCode, List<ItemInfo>>> CheckStageClearDataAsync(Int64 userId, Int64 stageCode)
    {
        var stageItemKey = "Stage_" + userId + "_Item";
        var stageEnemyKey = "Stage_" + userId + "_Enemy";

        try
        {
            var enemyRedis = new RedisString<Tuple<List<KilledStageEnemy>, Int64>>(_redisConn, stageEnemyKey, null);
            var enemyResult = await enemyRedis.GetAsync();

            if (enemyResult.HasValue == false)
            {
                return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.CheckStageClearDataFailWrongKey, null);
            }

            if (enemyResult.Value.Item2 != stageCode)
            {                
                return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.CheckStageClearDataFailWrongStage, null);
            }

            var enemyList = enemyResult.Value.Item1;

            foreach (StageEnemy stageEnemy in _masterDb.StageEnemyInfo.FindAll(i => i.Code == stageCode))
            {
                if (enemyList.Find(i => i.EnemyCode == stageEnemy.NpcCode && i.EnemyCount == stageEnemy.Count) == null)
                {
                    return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.CheckStageClearDataFailWrongData, null);
                }
            }

            // 획득한 아이템 정보 가져오기
            var itemRedis = new RedisString<Tuple<List<ItemInfo>, Int64>>(_redisConn, stageItemKey, null);
            var itemResult = await itemRedis.GetAsync();

            if (itemResult.Value.Item2 != stageCode)
            {
                return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.CheckStageClearDataFailWrongStage, null);
            }

            var itemList = itemResult.Value.Item1;

            return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.None, itemList);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "CheckStageClearData Exception");
            
            return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.CheckStageClearDataFailException, null);
        }
    }
}

