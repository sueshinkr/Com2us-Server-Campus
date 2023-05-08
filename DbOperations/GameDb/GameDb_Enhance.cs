using System;
using SqlKata.Execution;
using MySqlConnector;
using WebAPIServer.DataClass;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class GameDb : IGameDb
{
    // 아이템 강화
    // User_Item 테이블 업데이트 및 User_Item_EnhanceHistory 테이블에 데이터 추가
    public async Task<Tuple<ErrorCode, UserItem>> EnhanceItemAsync(Int64 userId, Int64 itemId)
    {
        UserItem itemData = new UserItem();
        var enhancedAt = DateTime.Now;

        try
        {
            itemData = await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                          .Where("UserId", userId).Where("IsDestroyed", false)
                                          .FirstOrDefaultAsync<UserItem>();

            if (itemData == null)
            {
                _logger.ZLogError($"[EnhanceItem] ErrorCode: {ErrorCode.EnhanceItemFailWrongData}, ItemId: {itemId}");
                return new Tuple<ErrorCode, UserItem>(ErrorCode.EnhanceItemFailWrongData, null);
            }

            var enhanceData = _MasterDb.ItemInfo.Find(i => i.Code == itemData.ItemCode);

            if (enhanceData.EnhanceMaxCount == 0)
            {
                _logger.ZLogError($"[EnhanceItem] ErrorCode: {ErrorCode.EnhanceItemFailNotEnhanceable}, ItemId: {itemId}");
                return new Tuple<ErrorCode, UserItem>(ErrorCode.EnhanceItemFailNotEnhanceable, null);
            }
            else if (itemData.EnhanceCount == enhanceData.EnhanceMaxCount)
            {
                _logger.ZLogError($"[EnhanceItem] ErrorCode: {ErrorCode.EnhanceItemFailAlreadyMax}, ItemId: {itemId}");
                return new Tuple<ErrorCode, UserItem>(ErrorCode.EnhanceItemFailAlreadyMax, null);
            }

            var hasEnoughMoney = await _queryFactory.Query("User_Data").Where("UserId", userId)
                                        .Where("Money", ">", (itemData.EnhanceCount + 1) * 10)
                                        .DecrementAsync("Money", (int)(itemData.EnhanceCount + 1) * 10);

            if (hasEnoughMoney == 0)
            {
                _logger.ZLogError($"[EnhanceItem] ErrorCode: {ErrorCode.EnhanceItemFailNotEnoughMoney}, ItemId: {itemId}");
                return new Tuple<ErrorCode, UserItem>(ErrorCode.EnhanceItemFailNotEnoughMoney, null);
            }

            var random = new Random();
            var isSuccess = random.NextDouble() < 0.85;

            if (isSuccess == true)
            {
                if (enhanceData.Attribute == 1) // 무기
                {
                    await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                       .UpdateAsync(new
                                       {
                                           EnhanceCount = itemData.EnhanceCount + 1,
                                           Attack = (Int64)Math.Round(itemData.Attack * 1.1)
                                       });
                }
                else if (enhanceData.Attribute == 2) // 방어구
                {
                    await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                       .UpdateAsync(new
                                       {
                                           EnhanceCount = itemData.EnhanceCount + 1,
                                           Defence = (Int64)Math.Round(itemData.Defence * 1.1)
                                       });
                }

                await _queryFactory.Query("User_Item_EnhanceHistory")
                                   .InsertAsync(new
                                   {
                                       ItemId = itemId,
                                       EnhanceCount_Before = itemData.EnhanceCount,
                                       IsSuccess = true,
                                       EnhancedAt = enhancedAt
                                   });

                itemData.EnhanceCount++;
                return new Tuple<ErrorCode, UserItem>(ErrorCode.None, itemData);
            }
            else
            {
                await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                   .UpdateAsync(new { IsDestroyed = true });

                await _queryFactory.Query("User_Item_EnhanceHistory")
                                   .InsertAsync(new
                                   {
                                       ItemId = itemId,
                                       EnhanceCount_Before = itemData.EnhanceCount,
                                       IsSuccess = false,
                                       EnhancedAt = enhancedAt
                                   });

                itemData.IsDestoryed = true;
                return new Tuple<ErrorCode, UserItem>(ErrorCode.None, itemData);
            }
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
                                   IsDestroyed = itemData.IsDestoryed
                               });

            await _queryFactory.Query("User_Item_EnhanceHistory").Where("ItemId", itemId).Where("EnhancedAt", enhancedAt).DeleteAsync();

            _logger.ZLogError(ex, $"[EnhanceItem] ErrorCode: {ErrorCode.EnhanceItemFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, UserItem>(ErrorCode.EnhanceItemFailException, null);
        }
    }

}
