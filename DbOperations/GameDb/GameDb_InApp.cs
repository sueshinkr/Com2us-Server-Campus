using System;
using SqlKata.Execution;
using MySqlConnector;
using WebAPIServer.DataClass;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class GameDb : IGameDb
{
    // 인앱 결제 확인
    // InAppReceipt 테이블에 데이터 추가, 유저에게 상품 메일 전송
    public async Task<ErrorCode> PurchaseInAppProductAsync(Int64 userId, Int64 purchaseId, Int64 productCode)
    {
        try
        {
            await _queryFactory.Query("InAppReceipt").InsertAsync(new
            {
                PurchaseId = purchaseId,
                UserId = userId,
                ProductCode = productCode
            });

            // 메일 전송
            var errorCode = await SendMailInAppProduct(userId, productCode);
            if (errorCode != ErrorCode.None)
            {
                // 롤백
                await _queryFactory.Query("InAppReceipt").Where("PurchaseId", purchaseId)
                                   .DeleteAsync();

                return errorCode;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            if (ex is MySqlException mysqlEx && mysqlEx.Number == 1062)
            {
                _logger.ZLogError(mysqlEx, $"[PurchaseInAppProduct] ErrorCode: {ErrorCode.PurchaseInAppProductFailDuplicate}, PurchaseId: {purchaseId}, ErrorNum : {mysqlEx.Number}");
                return ErrorCode.PurchaseInAppProductFailDuplicate;

            }
            else
            {
                _logger.ZLogError(ex, $"[PurchaseInAppProduct] ErrorCode: {ErrorCode.PurchaseInAppProductFailException}, PurchaseId: {purchaseId}");
                return ErrorCode.PurchaseInAppProductFailException;
            }
        }
    }

    // 인앱 결제 상품 메일 전송
    // Mail_data 및 Mail_Item 테이블에 데이터 추가
    public async Task<ErrorCode> SendMailInAppProduct(Int64 userId, Int64 purchaseCode)
    {
        var mailid = _idGenerator.CreateId();

        try
        {
            await _queryFactory.Query("Mail_Data").InsertAsync(new
            {
                MailId = mailid,
                UserId = userId,
                SenderId = 0,
                Title = $"상품 {purchaseCode} 아이템 지급",
                Content = $"구매하신 상품 {purchaseCode}에 포함된 아이템을 지급해드립니다. 아이템 수령시 해당 결제에 대한 환불이 불가능함에 유의해주시기 바랍니다.",
                hasItem = true,
                ExpiredAt = DateTime.Now.AddYears(10)
            });

            foreach (InAppProduct product in _masterDb.InAppProductInfo.FindAll(i => i.Code == purchaseCode))
            {
                var itemid = _idGenerator.CreateId();

                await _queryFactory.Query("Mail_Item").InsertAsync(new
                {
                    ItemId = itemid,
                    MailId = mailid,
                    ItemCode = product.ItemCode,
                    ItemCount = product.ItemCount
                });
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            // 롤백
            await _queryFactory.Query("Mail_Data").Where("MailId", mailid).DeleteAsync();
            await _queryFactory.Query("Mail_Item").Where("MailId", mailid).DeleteAsync();

            _logger.ZLogError(ex, $"[SendMailInAppProduct] ErrorCode: {ErrorCode.SendMailInAppProductFailException}, UserId: {userId}");
            return ErrorCode.SendMailInAppProductFailException;
        }
    }

}
