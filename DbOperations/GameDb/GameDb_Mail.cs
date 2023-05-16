using System;
using SqlKata.Execution;
using WebAPIServer.DataClass;
using WebAPIServer.DbOperations;
using WebAPIServer.Util;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class GameDb : IGameDb
{
    // 메일 기본 데이터 로딩
    // Mail_Data 테이블에서 메일 기본 정보 가져오기
    public async Task<Tuple<ErrorCode, List<MailData>>> LoadMailDataAsync(Int64 userId, Int64 pageNumber)
    {
        var mailData = new List<MailData>();
        var mailsPerPage = _defaultSetting.MailsPerPage;

        if (pageNumber < 1)
        {
            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.LoadMailDataFailWrongPage, mailData);
        }

        try
        {
            mailData = await _queryFactory.Query("Mail_Data").Where("UserId", userId).Where("IsDeleted", false)
                                          .OrderByDesc("MailId").Offset((pageNumber - 1) * mailsPerPage).Limit((int)mailsPerPage)
                                          .GetAsync<MailData>() as List<MailData>;

            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.None, mailData);
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.LoadMailDataFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "LoadMailData Exception");

            return new Tuple<ErrorCode, List<MailData>>(errorCode, mailData);
        }
    }

    // 메일 본문 및 포함 아이템 읽기
    // Mail_Data 테이블에서 본문내용, Mail_Item 테이블에서 아이템 정보 가져오기
    public async Task<Tuple<ErrorCode, string, List<MailItem>>> ReadMailAsync(Int64 mailId, Int64 userId)
    {
        try
        {
            var content = await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                                             .Where("UserId", userId).Where("IsDeleted", false)
                                             .Select("Content").FirstOrDefaultAsync<string>();

            if (content == null)
            {
                return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.ReadMailFailWrongData, null, null);
            }

            var mailItem = await _queryFactory.Query("Mail_Item").Where("MailId", mailId)
                                              .Select("ItemId", "ItemCode", "ItemCount", "IsReceived")
                                              .GetAsync<MailItem>() as List<MailItem>;

            await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                               .UpdateAsync(new { IsRead = true });

            return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.None, content, mailItem);
        }
        catch (Exception ex)
        {
            // 롤백
            await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                               .UpdateAsync(new { IsRead = false });

            var errorCode = ErrorCode.ReadMailFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "ReadMailFail Exception");

            return new Tuple<ErrorCode, string, List<MailItem>>(errorCode, null, null);
        }
    }

    // 메일 아이템 수령
    // Mail_Item 테이블에서 아이템 정보 가져와서 User_Item 테이블에 추가
    public async Task<Tuple<ErrorCode, List<ItemInfo>>> ReceiveMailItemAsync(Int64 mailId, Int64 userId)
    {
        var itemInfo = new List<ItemInfo>();
        var errorCode = new ErrorCode();

        try
        {
            var isCorrectRequest = await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                                                      .Where("UserId", userId).Where("IsDeleted", false)
                                                      .ExistsAsync();

            if (isCorrectRequest == false)
            {
                return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.ReceiveMailItemFailWrongData, null);
            }

            var mailItem = await _queryFactory.Query("Mail_Item").Where("MailId", mailId)
                                              .GetAsync<MailItem>() as List<MailItem>;

            foreach (MailItem item in mailItem)
            {
                (errorCode, var newItem) = await InsertUserItemAsync(userId, item.ItemCode, item.ItemCount, item.ItemId);

                if (errorCode != ErrorCode.None)
                {
                    break;
                }

                itemInfo.Add(newItem);
            }

            if (errorCode != ErrorCode.None)
            {
                // 롤백
                foreach (ItemInfo item in itemInfo)
                {
                    await DeleteUserItemAsync(userId, item.ItemId, item.ItemCount);
                }

                return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.ReceiveMailItemFailInsertItem, null);
            }

            await _queryFactory.Query("Mail_Item").Where("MailId", mailId)
                               .UpdateAsync(new { IsReceived = true });

            await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                               .UpdateAsync(new { HasItem = false });

            return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.None, itemInfo);
        }
        catch (Exception ex)
        {
            //롤백
            foreach(ItemInfo item in itemInfo)
            {
                await DeleteUserItemAsync(userId, item.ItemId, item.ItemCount);
            }

            await _queryFactory.Query("Mail_Item").Where("MailId", mailId)
                               .UpdateAsync(new { IsReceived = false });

            await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                               .UpdateAsync(new { HasItem = true });

            errorCode = ErrorCode.ReceiveMailItemFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "ReceiveMailItem Exception");

            return new Tuple<ErrorCode, List<ItemInfo>>(errorCode, itemInfo);
        }
    }

    // 메일 삭제
    // Mail_Data 테이블에서 논리적으로만 삭제
    public async Task<ErrorCode> DeleteMailAsync(Int64 mailId, Int64 userId)
    {
        try
        {
            var isCorrectRequest = await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                                                      .Where("UserId", userId).Where("IsDeleted", false)
                                                      .Select("UserId", "IsDeleted").ExistsAsync();

            if (isCorrectRequest == false)
            {
                return ErrorCode.DeleteMailFailWrongData;
            }
            else
            {
                await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                                   .UpdateAsync(new { IsDeleted = true });

                return ErrorCode.None;
            }
        }
        catch (Exception ex)
        {
            //롤백
            await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                               .UpdateAsync(new { IsDeleted = false });

            var errorCode = ErrorCode.DeleteMailFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "DeleteMail Exception");

            return errorCode;
        }
    }

    // 메일에 아이템 첨부
    // Mail_Item 테이블에 데이터 추가
    private async Task<ErrorCode> InsertItemIntoMailAsync(Int64 mailId, Int64 itemCode, Int64 itemCount)
    {
        try
        {
            Int64 itemId;

            itemId = _idGenerator.CreateId();

            await _queryFactory.Query("Mail_Item").InsertAsync(new
            {
                ItemId = itemId,
                MailId = mailId,
                ItemCode = itemCode,
                ItemCount = itemCount
            });

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.InsertItemIntoMailFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "InsertItemIntoMail Exception");

            return errorCode;
        }
    }
}
