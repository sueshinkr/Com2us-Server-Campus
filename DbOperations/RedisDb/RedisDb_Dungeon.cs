using System;
using CloudStructures.Structures;
using Org.BouncyCastle.Asn1.Pkcs;
using StackExchange.Redis;
using WebAPIServer.DataClass;
using WebAPIServer.Log;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class RedisDb : IRedisDb
{
    // 스테이지 진행 정보 생성
    // UserId로 키 생성
    public async Task<ErrorCode> CreateStageProgressDataAsync(Int64 userId, Int64 stageCode)
    {
        var stageItemKey = "User_" + userId + "_StageItem";
        var stageEnemyKey = "User_" + userId + "_StageEnemy";

        try
        {
            var itemRedis = new RedisString<ObtainedStageItem>(_redisConn, stageItemKey, null);
            var enemyRedis = new RedisString<KilledStageEnemy>(_redisConn, stageEnemyKey, null);

            var itemRedisResult = await itemRedis.GetAsync();
            var enemyRedisResult = await enemyRedis.GetAsync();

            if (itemRedisResult.HasValue == true || enemyRedisResult.HasValue == true)
            {
                await DeleteStageProgressDataAsync(userId);

                return ErrorCode.CreateStageProgressDataFailAlreadyIn;
            }
            else
            {
                var item = new ObtainedStageItem { obtainedItemList = new List<ItemInfo>(), StageCode = stageCode };
                var enemy = new KilledStageEnemy { KilledEnemyList = new List<KilledEnemy>(), StageCode = stageCode };

                if (await itemRedis.SetAsync(item) == false || await enemyRedis.SetAsync(enemy) == false)
                {
                    await DeleteStageProgressDataAsync(userId);
                    return ErrorCode.CreateStageProgressDataFailRedis;
                }

                return ErrorCode.None;
            }
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.CreateStageProgressDataFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "CreateStageProgressData Exception");

            return errorCode;
        }
    }

    // 스테이지 진행 정보 삭제
    // UserId에 해당하는 키 제거
    public async Task<ErrorCode> DeleteStageProgressDataAsync(Int64 userId)
    {
        var stageItemKey = "User_" + userId + "_StageItem";
        var stageEnemyKey = "User_" + userId + "_StageEnemy";

        try
        {
            var itemRedis = new RedisString<ObtainedStageItem>(_redisConn, stageItemKey, null);
            var enemyRedis = new RedisString<KilledStageEnemy>(_redisConn, stageEnemyKey, null);

            if (await itemRedis.DeleteAsync() == false | await enemyRedis.DeleteAsync() == false)
            {
                return ErrorCode.DeleteStageProgressDataFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.DeleteStageProgressDataFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "DeleteStageProgressData Exception");

            return errorCode;
        }
    }

    // 스테이지 아이템 획득
    // 유저의 stageItemKey에 아이템 추가
    public async Task<ErrorCode> ObtainItemAsync(Int64 userId, Int64 itemCode, Int64 itemCount)
    {
        var stageItemKey = "User_" + userId + "_StageItem";

        try
        {
            var obtainedItemRedis = new RedisString<ObtainedStageItem>(_redisConn, stageItemKey, null);
            var obtainedItemRedisResult = await obtainedItemRedis.GetAsync();

            if (obtainedItemRedisResult.HasValue == false)
            {
                return ErrorCode.ObtainItemFailWrongKey;
            }

            var stageCode = obtainedItemRedisResult.Value.StageCode;

            if (_masterDb.StageItemInfo.Find(i => i.Code == stageCode && i.ItemCode == itemCode) == null)
            {
                return ErrorCode.ObtainItemFailWrongItem;
            }

            var obtainedItemList = obtainedItemRedisResult.Value.obtainedItemList;
            var index = obtainedItemList.FindIndex(i => i.ItemCode == itemCode);

            if (index >= 0)
            {
                obtainedItemList[index].ItemCount += itemCount;
            }
            else
            {
                obtainedItemList.Add(new ItemInfo { ItemCode = itemCode, ItemCount = itemCount });
            }

            if (await obtainedItemRedis.SetAsync(obtainedItemRedisResult.Value) == false)
            {
                return ErrorCode.ObtainItemFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.ObtainItemFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "ObtainItem Exception");

            return errorCode;
        }
    }

    // 스테이지 적 제거 
    // 유저의 stageEnemyKey에 적 추가
    public async Task<ErrorCode> KillEnemyAsync(Int64 userId,Int64 enemyCode)
    {
        var stageEnemyKey = "User_" + userId + "_StageEnemy";

        try
        {
            var killedEnemyRedis = new RedisString<KilledStageEnemy>(_redisConn, stageEnemyKey, null);
            var killedEnemyRedisResult = await killedEnemyRedis.GetAsync();

            if (killedEnemyRedisResult.HasValue == false)
            {
                return ErrorCode.KillEnemyFailWrongKey;
            }

            var stageCode = killedEnemyRedisResult.Value.StageCode;

            if (_masterDb.StageEnemyInfo.Find(i => i.Code == stageCode && i.NpcCode == enemyCode) == null)
            {
                return ErrorCode.KillEnemyFailWrongEnemy;
            }

            var killedEnemyList = killedEnemyRedisResult.Value.KilledEnemyList;
            var index = killedEnemyList.FindIndex(i => i.EnemyCode == enemyCode);

            if (index >= 0)
            {
                killedEnemyList[index].EnemyCount += 1;
            }
            else
            {
                killedEnemyList.Add(new KilledEnemy { EnemyCode = enemyCode, EnemyCount = 1 });
            }

            if (await killedEnemyRedis.SetAsync(killedEnemyRedisResult.Value) == false)
            {
                return ErrorCode.KillEnemyFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.KillEnemyFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "KillEnemy Exception");

            return errorCode;
        }
    }

    // 스테이지 클리어 확인
    // MasterData의 StageEnemy 데이터와 redis에 저장해놓은 데이터 비교 
    public async Task<Tuple<ErrorCode, List<ItemInfo>, Int64>> CheckStageClearDataAsync(Int64 userId)
    {
        var stageItemKey = "User_" + userId + "_StageItem";
        var stageEnemyKey = "User_" + userId + "_StageEnemy";

        try
        {
            var enemyRedis = new RedisString<KilledStageEnemy>(_redisConn, stageEnemyKey, null);
            var enemyRedisResult = await enemyRedis.GetAsync();

            if (enemyRedisResult.HasValue == false)
            {
                return new Tuple<ErrorCode, List<ItemInfo>, Int64>(ErrorCode.CheckStageClearDataFailWrongKey, null, 0);
            }

            var stageCode = enemyRedisResult.Value.StageCode;
            var enemyList = enemyRedisResult.Value.KilledEnemyList;

            foreach (StageEnemy stageEnemy in _masterDb.StageEnemyInfo.FindAll(i => i.Code == stageCode))
            {
                if (enemyList.Find(i => i.EnemyCode == stageEnemy.NpcCode && i.EnemyCount == stageEnemy.Count) == null)
                {
                    return new Tuple<ErrorCode, List<ItemInfo>, Int64>(ErrorCode.CheckStageClearDataFailWrongData, null, 0);
                }
            }

            // 획득한 아이템 정보 가져오기
            var itemRedis = new RedisString<ObtainedStageItem>(_redisConn, stageItemKey, null);
            var itemRedisResult = await itemRedis.GetAsync();

            if (itemRedisResult.HasValue == false)
            {
                return new Tuple<ErrorCode, List<ItemInfo>, Int64>(ErrorCode.CheckStageClearDataFailWrongKey, null, 0);
            }

            var itemList = itemRedisResult.Value.obtainedItemList;

            foreach (ItemInfo item in itemList)
            {
                if (_masterDb.StageItemInfo.Find(i => i.ItemCode == item.ItemCode && i.Count >= item.ItemCount) == null)
                {
                    return new Tuple<ErrorCode, List<ItemInfo>, Int64>(ErrorCode.CheckStageClearDataFailWrongData, null, 0);
                }
            }

            return new Tuple<ErrorCode, List<ItemInfo>, Int64>(ErrorCode.None, itemList, stageCode);
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.CheckStageClearDataFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "CheckStageClearData Exception");
            
            return new Tuple<ErrorCode, List<ItemInfo>, Int64>(errorCode, null, 0);
        }
    }
}

