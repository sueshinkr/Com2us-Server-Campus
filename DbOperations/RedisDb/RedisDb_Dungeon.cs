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
            var itemRedis = new RedisString<Tuple<List<ObtainedStageItem>, Int64>>(_redisConn, stageItemKey, null);
            var enemyRedis = new RedisString<Tuple<List<KilledStageEnemy>, Int64>>(_redisConn, stageEnemyKey, null);

            var item = await itemRedis.GetAsync();
            var enemy = await enemyRedis.GetAsync();

            if (item.HasValue == true || enemy.HasValue == true)
            {
                await DeleteStageProgressDataAsync(userId, stageCode);

                _logger.ZLogError($"[CreateStageProgressData] ErrorCode: {ErrorCode.CreateStageProgressDataFailAlreadyIn}, UserId: {userId}");
                return ErrorCode.CreateStageProgressDataFailAlreadyIn;
            }
            else
            {
                var itemList = new Tuple<List<ObtainedStageItem>, Int64>(new List<ObtainedStageItem>(), stageCode);
                var enemyList = new Tuple<List<KilledStageEnemy>, Int64>(new List<KilledStageEnemy>(), stageCode);

                if (await itemRedis.SetAsync(itemList) == false || await enemyRedis.SetAsync(enemyList) == false)
                {
                    _logger.ZLogError($"[CreateStageProgressData] ErrorCode: {ErrorCode.CreateStageProgressDataFailRedis}, UserId: {userId}");
                    return ErrorCode.CreateStageProgressDataFailRedis;
                }

                return ErrorCode.None;
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[CreateStageProgressData] ErrorCode: {ErrorCode.CreateStageProgressDataFailException}, UserId: {userId}");
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
            var itemRedis = new RedisString<Tuple<List<ObtainedStageItem>, Int64>>(_redisConn, stageItemKey, null);
            var enemyRedis = new RedisString<Tuple<List<KilledStageEnemy>, Int64>>(_redisConn, stageEnemyKey, null);

            if (await itemRedis.DeleteAsync() == false | await enemyRedis.DeleteAsync() == false)
            {
                _logger.ZLogError($"[DeleteStageProgressData] ErrorCode: {ErrorCode.CreateStageProgressDataFailRedis}, UserId: {userId}");
                return ErrorCode.DeleteStageProgressDataFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[DeleteStageProgressData] ErrorCode: {ErrorCode.DeleteStageProgressDataFailException}, UserId: {userId}");
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
            _logger.ZLogError($"[ObtainItem] ErrorCode: {ErrorCode.ObtainItemFailWrongItem}, UserId: {userId}");
            return ErrorCode.ObtainItemFailWrongItem;
        }

        var stageItemKey = "Stage_" + userId + "_Item";

        try
        {
            var redis = new RedisString<Tuple<List<ObtainedStageItem>, Int64>>(_redisConn, stageItemKey, null);
            var itemResult = await redis.GetAsync();

            if (itemResult.HasValue == false)
            {
                _logger.ZLogError($"[ObtainItem] ErrorCode: {ErrorCode.ObtainItemFailWrongKey}, UserId: {userId}");
                return ErrorCode.ObtainItemFailWrongKey;
            }

            if (itemResult.Value.Item2 != stageCode)
            {
                _logger.ZLogError($"[ObtainItem] ErrorCode: {ErrorCode.ObtainItemFailWrongStage}, UserId: {userId}");
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
                itemlist.Add(new ObtainedStageItem(itemCode, itemCount));
            }

            if (await redis.SetAsync(new Tuple<List<ObtainedStageItem>, Int64>(itemlist, stageCode)) == false)
            {
                _logger.ZLogError($"[ObtainItem] ErrorCode: {ErrorCode.ObtainItemFailRedis}, UserId: {userId}");
                return ErrorCode.ObtainItemFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[ObtainItem] ErrorCode: {ErrorCode.ObtainItemFailException}, UserId: {userId}");
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
            _logger.ZLogError($"[KillEnemy] ErrorCode: {ErrorCode.KillEnemyFailWrongEnemy}, UserId: {userId}");
            return ErrorCode.KillEnemyFailWrongEnemy;
        }

        var stageEnemyKey = "Stage_" + userId + "_Enemy";

        try
        {
            var redis = new RedisString<Tuple<List<KilledStageEnemy>, Int64>>(_redisConn, stageEnemyKey, null);
            var enemyResult = await redis.GetAsync();

            if (enemyResult.HasValue == false)
            {
                _logger.ZLogError($"[KillEnemy] ErrorCode: {ErrorCode.KillEnemyFailWrongKey}, UserId: {userId}");
                return ErrorCode.KillEnemyFailWrongKey;
            }

            if (enemyResult.Value.Item2 != stageCode)
            {
                _logger.ZLogError($"[KillEnemy] ErrorCode: {ErrorCode.KillEnemyFailWrongStage}, UserId: {userId}");
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
                _logger.ZLogError($"[KillEnemy] ErrorCode: {ErrorCode.KillEnemyFailRedis}, UserId: {userId}");

                return ErrorCode.KillEnemyFailRedis;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[KillEnemy] ErrorCode: {ErrorCode.KillEnemyFailException}, UserId: {userId}");
            return ErrorCode.KillEnemyFailException;
        }
    }

    // 스테이지 클리어 확인
    // MasterData의 StageEnemy 데이터와 redis에 저장해놓은 데이터 비교 
    public async Task<Tuple<ErrorCode, List<ObtainedStageItem>>> CheckStageClearAsync(Int64 userId, Int64 stageCode)
    {
        var stageItemKey = "Stage_" + userId + "_Item";
        var stageEnemyKey = "Stage_" + userId + "_Enemy";

        try
        {
            var enemyRedis = new RedisString<Tuple<List<KilledStageEnemy>, Int64>>(_redisConn, stageEnemyKey, null);
            var enemyResult = await enemyRedis.GetAsync();

            if (enemyResult.HasValue == false)
            {
                _logger.ZLogError($"[CheckStageClear] ErrorCode: {ErrorCode.CheckStageClearFailWrongKey}, UserId: {userId}");
                return new Tuple<ErrorCode, List<ObtainedStageItem>>(ErrorCode.CheckStageClearFailWrongKey, null);
            }

            if (enemyResult.Value.Item2 != stageCode)
            {
                _logger.ZLogError($"[CheckStageClear] ErrorCode: {ErrorCode.CheckStageClearFailWrongStage}, UserId: {userId}");
                return new Tuple<ErrorCode, List<ObtainedStageItem>>(ErrorCode.CheckStageClearFailWrongStage, null);
            }

            var enemyList = enemyResult.Value.Item1;

            foreach (StageEnemy stageEnemy in _masterDb.StageEnemyInfo.FindAll(i => i.Code == stageCode))
            {
                if (enemyList.Find(i => i.EnemyCode == stageEnemy.NpcCode && i.EnemyCount == stageEnemy.Count) == null)
                {
                    _logger.ZLogError($"[CheckStageClear] ErrorCode: {ErrorCode.CheckStageClearFailWrongData}, UserId: {userId}");
                    return new Tuple<ErrorCode, List<ObtainedStageItem>>(ErrorCode.CheckStageClearFailWrongData, null);
                }
            }

            // 획득한 아이템 정보 가져오
            var itemRedis = new RedisString<Tuple<List<ObtainedStageItem>, Int64>>(_redisConn, stageItemKey, null);
            var itemResult = await itemRedis.GetAsync();
            var itemList = itemResult.Value.Item1;

            return new Tuple<ErrorCode, List<ObtainedStageItem>>(ErrorCode.None, itemList);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[CheckStageClear] ErrorCode: {ErrorCode.CheckStageClearFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, List<ObtainedStageItem>>(ErrorCode.CheckStageClearFailException, null);
        }
    }
}

