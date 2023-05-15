using System;
using SqlKata.Execution;
using MySqlConnector;
using WebAPIServer.DataClass;
using ZLogger;
using WebAPIServer.Log;

namespace WebAPIServer.DbOperations;

public partial class GameDb : IGameDb
{
    // 아이템 강화
    // User_Item 테이블 업데이트 및 User_Item_EnhanceHistory 테이블에 데이터 추가
    public async Task<Tuple<ErrorCode, UserItem>> EnhanceItemAsync(Int64 userId, Int64 itemId)
    {
        UserItem itemData = new UserItem();

        try
        {
            (var errorCode, itemData, var enhanceData) = await CheckEnhanceableAsync(userId, itemId);

            if (errorCode != ErrorCode.None)
            {
                return new Tuple<ErrorCode, UserItem>(errorCode, itemData);
            }

            var isSuccess = DetermineEnhancementResult();

            if (isSuccess == true)
            {
                errorCode = await HandleSuccessfulEnhancementAsync(itemId, itemData, enhanceData.Attribute);
            }
            else
            {
                errorCode = await HandleFailedEnhancementAsync(itemId, itemData);
            }

            if (errorCode != ErrorCode.None)
            {
                // 롤백
                await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                   .UpdateAsync(new
                                   {
                                       Attack = itemData.Attack,
                                       Defence = itemData.Defence,
                                       EnhanceCount = itemData.EnhanceCount,
                                       IsDestroyed = itemData.IsDestroyed
                                   });

                return new Tuple<ErrorCode, UserItem>(errorCode, null);
            }

            return new Tuple<ErrorCode, UserItem>(ErrorCode.None, itemData);
        }
        catch (Exception ex)
        {
            // 롤백
            await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                               .UpdateAsync(new
                               {
                                   Attack = itemData.Attack,
                                   Defence = itemData.Defence,
                                   EnhanceCount = itemData.EnhanceCount,
                                   IsDestroyed = itemData.IsDestroyed
                               });

            var errorCode = ErrorCode.EnhanceItemFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "EnhanceItem Exception");

            return new Tuple<ErrorCode, UserItem>(errorCode, null);
        }
    }

    // 강화 가능 여부 체크
    // User_Item, User_Data 테이블 데이터 검증
    private async Task<Tuple<ErrorCode, UserItem, Item>> CheckEnhanceableAsync(Int64 userId, Int64 itemId)
    {
        try
        {
            var itemData = await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                              .Where("UserId", userId).Where("IsDestroyed", false)
                                              .FirstOrDefaultAsync<UserItem>();

            if (itemData == null)
            {
                return new Tuple<ErrorCode, UserItem, Item>(ErrorCode.CheckEnhanceableFailWrongData, itemData, null);
            }

            var enhanceData = _masterDb.ItemInfo.Find(i => i.Code == itemData.ItemCode);

            if (enhanceData.EnhanceMaxCount == 0)
            {
                return new Tuple<ErrorCode, UserItem, Item>(ErrorCode.CheckEnhanceableFailNotEnhanceable, null, null);
            }
            else if (itemData.EnhanceCount == enhanceData.EnhanceMaxCount)
            {
                return new Tuple<ErrorCode, UserItem, Item>(ErrorCode.CheckEnhanceableFailAlreadyMax, null, null);
            }

            var enhanceCost = (itemData.EnhanceCount + 1) * 10;
            var hasEnoughMoney = await _queryFactory.Query("User_Data").Where("UserId", userId)
                                        .Where("Money", ">", enhanceCost)
                                        .DecrementAsync("Money", (int)enhanceCost);

            if (hasEnoughMoney == 0)
            {
                return new Tuple<ErrorCode, UserItem, Item>(ErrorCode.CheckEnhanceableFailNotEnoughMoney, null, null);
            }

            return new Tuple<ErrorCode, UserItem, Item>(ErrorCode.None, itemData, enhanceData);
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.CheckEnhanceableFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "CheckEnhanceable Exception");

            return new Tuple<ErrorCode, UserItem, Item>(errorCode, null, null);
        }
    }

    // 강화 확률 실행
    private bool DetermineEnhancementResult()
    {
        var random = new Random();
        var isSuccess = random.NextDouble() < 0.85;

        return isSuccess;
    }

    // 강화 성공시 작업
    private async Task<ErrorCode> HandleSuccessfulEnhancementAsync(Int64 itemId, UserItem itemData, Int64 itemAttribute)
    {
        try
        {
            if (itemAttribute == 1) // 무기
            {
                await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                   .UpdateAsync(new
                                   {
                                       EnhanceCount = itemData.EnhanceCount + 1,
                                       Attack = (Int64)Math.Round(itemData.Attack * 1.1)
                                   });
            }
            else if (itemAttribute == 2) // 방어구
            {
                await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                   .UpdateAsync(new
                                   {
                                       EnhanceCount = itemData.EnhanceCount + 1,
                                       Defence = (Int64)Math.Round(itemData.Defence * 1.1)
                                   });
            }

            itemData.EnhanceCount++;

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.HandleSuccessfulEnhancementFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "HandleSuccessfulEnhancement Exception");

            return errorCode;
        }
    }

    // 강화 실패시 작업
    private async Task<ErrorCode> HandleFailedEnhancementAsync(Int64 itemId, UserItem itemData)
    {
        try
        {
            await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                               .UpdateAsync(new { IsDestroyed = true });

            itemData.IsDestroyed = true;

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.HandleFailedEnhancementFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "HandleFailedEnhancement Exception");

            return errorCode;
        }
    }
}
