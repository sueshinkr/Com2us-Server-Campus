using System;
using CloudStructures.Structures;
using WebAPIServer.DataClass;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class RedisDb : IRedisDb
{
    // 스테이지 진행 정보 생성
    // UserId로 키 생성
    public async Task<ErrorCode> CreateStageProgressDataAsync(Int64 userId, Int64 stageCode)
    {
        var stageItemKey = "Stage" + stageCode + "_" + userId + "_Item";
        var stageEnemyKey = "Stage" + stageCode + "_" + userId + "_Enemy";

        try
        {
            var itemRedis = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageItemKey, null);
            var enemyRedis = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageEnemyKey, null);

            var item = await itemRedis.GetAsync();
            var enemy = await enemyRedis.GetAsync();

            if (item.HasValue == true || enemy.HasValue == true)
            {
                _logger.ZLogError($"[CreateStageProgressData] ErrorCode: {ErrorCode.CreateStageProgressDataFailAlreadyIn}, UserId: {userId}");
                return ErrorCode.CreateStageProgressDataFailAlreadyIn;
            }
            else
            {
                var itemList = new List<Tuple<Int64, Int64>>();
                var enemyList = new List<Tuple<Int64, Int64>>();

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
        var stageItemKey = "Stage" + stageCode + "_" + userId + "_Item";
        var stageEnemyKey = "Stage" + stageCode + "_" + userId + "_Enemy";

        try
        {
            var itemRedis = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageItemKey, null);
            var enemyRedis = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageEnemyKey, null);

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

        var stageItemKey = "Stage" + stageCode + "_" + userId + "_Item";

        try
        {
            var redis = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageItemKey, null);
            var itemResult = await redis.GetAsync();

            if (itemResult.HasValue == false)
            {
                _logger.ZLogError($"[ObtainItem] ErrorCode: {ErrorCode.ObtainItemFailWrongData}, UserId: {userId}");
                return ErrorCode.ObtainItemFailWrongData;
            }

            var itemlist = itemResult.Value;
            var index = itemlist.FindIndex(i => i.Item1 == itemCode);

            if (index >= 0)
            {
                itemlist[index] = new Tuple<Int64, Int64>(itemCode, itemlist[index].Item2 + itemCount);
            }
            else
            {
                itemlist.Add(new Tuple<Int64, Int64>(itemCode, itemCount));
            }

            if (await redis.SetAsync(itemlist) == false)
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

        var stageEnemyKey = "Stage" + stageCode + "_" + userId + "_Enemy";

        try
        {
            var redis = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageEnemyKey, null);
            var enemyResult = await redis.GetAsync();

            if (enemyResult.HasValue == false)
            {
                _logger.ZLogError($"[KillEnemy] ErrorCode: {ErrorCode.KillEnemyFailWrongData}, UserId: {userId}");
                return ErrorCode.KillEnemyFailWrongData;
            }

            var enemyList = enemyResult.Value;
            if (enemyList == null)
            {
                enemyList = new List<Tuple<Int64, Int64>>();
                enemyList.Add(new Tuple<Int64, Int64>(enemyCode, 1));
            }
            else
            {
                var index = enemyList.FindIndex(i => i.Item1 == enemyCode);

                if (index >= 0)
                {
                    enemyList[index] = new Tuple<Int64, Int64>(enemyCode, enemyList[index].Item2 + 1);
                }
                else
                {
                    enemyList.Add(new Tuple<Int64, Int64>(enemyCode, 1));
                }
            }

            if (await redis.SetAsync(enemyList) == false)
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
    public async Task<Tuple<ErrorCode, List<Tuple<Int64, Int64>>>> CheckStageClearAsync(Int64 userId, Int64 stageCode)
    {
        var stageItemKey = "Stage" + stageCode + "_" + userId + "_Item";
        var stageEnemyKey = "Stage" + stageCode + "_" + userId + "_Enemy";

        try
        {
            var redis = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageEnemyKey, null);
            var enemyResult = await redis.GetAsync();

            if (enemyResult.HasValue == false)
            {
                _logger.ZLogError($"[CheckStageClear] ErrorCode: {ErrorCode.CheckStageClearFailWrongData}, UserId: {userId}");
                return new Tuple<ErrorCode, List<Tuple<Int64, Int64>>>(ErrorCode.CheckStageClearFailWrongData, null);
            }

            var enemyList = enemyResult.Value;

            foreach (StageEnemy stageEnemy in _masterDb.StageEnemyInfo.FindAll(i => i.Code == stageCode))
            {
                if (enemyList.Find(i => i.Item1 == stageEnemy.NpcCode && i.Item2 == stageEnemy.Count) == null)
                {
                    _logger.ZLogError($"[CheckStageClear] ErrorCode: {ErrorCode.CheckStageClearFailWrongData}, UserId: {userId}");
                    return new Tuple<ErrorCode, List<Tuple<Int64, Int64>>>(ErrorCode.CheckStageClearFailWrongData, null);
                }
            }

            redis = new RedisString<List<Tuple<Int64, Int64>>>(_redisConn, stageItemKey, null);
            var itemResult = await redis.GetAsync();
            var itemList = itemResult.Value;

            return new Tuple<ErrorCode, List<Tuple<Int64, Int64>>>(ErrorCode.None, itemList);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[CheckStageClear] ErrorCode: {ErrorCode.CheckStageClearFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, List<Tuple<Int64, Int64>>>(ErrorCode.CheckStageClearFailException, null);
        }
    }
}

