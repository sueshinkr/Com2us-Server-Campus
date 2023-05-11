using System;
using SqlKata.Execution;
using WebAPIServer.DataClass;
using WebAPIServer.DbOperations;
using WebAPIServer.Log;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class GameDb : IGameDb
{
    // 메일 기본 데이터 로딩
    // Mail_Data 테이블에서 메일 기본 정보 가져오기
    public async Task<Tuple<ErrorCode, List<MailData>>> LoadMailDataAsync(Int64 userId, Int64 pageNumber)
    {
        var mailData = new List<MailData>();
        var mailsPerPage = int.Parse(_configuration.GetSection("MailSetting")["mailsPerPage"]);
        // 일단 나중에 처리 ...

        if (pageNumber < 1)
        {
            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.LoadMailDataFailWrongPage, mailData);
        }

        try
        {
            mailData = await _queryFactory.Query("Mail_Data").Where("UserId", userId).Where("IsDeleted", false)
                                          .OrderByDesc("MailId").Offset((pageNumber - 1) * mailsPerPage).Limit(mailsPerPage)
                                          .GetAsync<MailData>() as List<MailData>;

            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.None, mailData);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "LoadMailData Exception");

            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.LoadMailDataFailException, mailData);
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
            _logger.ZLogError(ex, "ReadMailFail Exception");

            return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.ReadMailFailException, null, null);
        }
    }

    // 메일 아이템 수령
    // Mail_Item 테이블에서 아이템 정보 가져와서 User_Item 테이블에 추가
    public async Task<Tuple<ErrorCode, List<ItemInfo>>> ReceiveMailItemAsync(Int64 mailId, Int64 userId)
    {
        var itemInfo = new List<ItemInfo>();

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
                (var errorCode, var newItem) = await InsertUserItemAsync(userId, item.ItemCode, item.ItemCount, item.ItemId);

                if (errorCode != ErrorCode.None)
                {
                    // 롤백
                    for (int i = 0; i <= mailItem.IndexOf(item); i++)
                    {
                        await DeleteUserItemAsync(userId, mailItem[i].ItemId, mailItem[i].ItemCount);
                    }

                    return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.ReceiveMailItemFailInsertItem, null);
                }

                itemInfo.Add(newItem);
            }

            await _queryFactory.Query("Mail_Item").Where("MailId", mailId)
                               .UpdateAsync(new { IsReceived = true });

            await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                               .UpdateAsync(new { HasItem = false });

            return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.None, itemInfo);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "ReceiveMailItem Exception");

            return new Tuple<ErrorCode, List<ItemInfo>>(ErrorCode.ReceiveMailItemFailException, itemInfo);
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
            _logger.ZLogError(ex, "DeleteMail Exception");

            return ErrorCode.DeleteMailFailException;
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
            _logger.ZLogError(ex, "InsertItemIntoMail Exception");

            return ErrorCode.InsertItemIntoMailFailException;
        }
    }
}
