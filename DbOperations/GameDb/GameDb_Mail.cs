using System;
using SqlKata.Execution;
using WebAPIServer.DataClass;
using WebAPIServer.DbOperations;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class GameDb : IGameDb
{
    // 메일 기본 데이터 로딩
    // Mail_Data 테이블에서 메일 기본 정보 가져오기
    public async Task<Tuple<ErrorCode, List<MailData>>> LoadMailDataAsync(Int64 userId, Int64 pagenumber)
    {
        var maildata = new List<MailData>();
        var mailsperpage = int.Parse(_configuration.GetSection("MailSetting")["MailsPerPage"]);

        if (pagenumber < 1)
        {
            _logger.ZLogError($"[LoadMailData] ErrorCode: {ErrorCode.LoadMailDataFailWrongPage}, UserId: {userId}, PageNumber: {pagenumber}");
            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.LoadMailDataFailWrongPage, maildata);
        }

        try
        {
            maildata = await _queryFactory.Query("Mail_Data").Where("UserId", userId).Where("IsDeleted", false)
                                          .OrderByDesc("ObtainedAt").Offset((pagenumber - 1) * mailsperpage).Limit(mailsperpage)
                                          .GetAsync<MailData>() as List<MailData>;

            if (maildata.Count == 0)
            {
                _logger.ZLogError($"[LoadMailData] ErrorCode: {ErrorCode.LoadMailDataFailNoData}, UserId: {userId}, PageNumber: {pagenumber}");
                return new Tuple<ErrorCode, List<MailData>>(ErrorCode.LoadMailDataFailNoData, maildata);
            }

            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.None, maildata);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[LoadMailData] ErrorCode: {ErrorCode.LoadMailDataFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.LoadMailDataFailException, maildata);
        }
    }

    // 메일 본문 및 포함 아이템 읽기
    // Mail_Data 테이블에서 본문내용, Mail_Item 테이블에서 아이템 정보 가져오기
    public async Task<Tuple<ErrorCode, string, List<MailItem>>> ReadMailAsync(Int64 mailid, Int64 userId)
    {
        try
        {
            var content = await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                                             .Where("UserId", userId).Where("IsDeleted", false).Select("Content")
                                             .FirstOrDefaultAsync<string>();

            if (content == null)
            {
                _logger.ZLogError($"[ReadMail] ErrorCode: {ErrorCode.ReadMailFailWrongData}, MailId: {mailid}");
                return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.ReadMailFailWrongData, null, null);
            }

            var mailItem = await _queryFactory.Query("Mail_Item").Where("MailId", mailid)
                                              .Select("ItemId", "ItemCode", "ItemCount", "IsReceived")
                                              .GetAsync<MailItem>() as List<MailItem>;

            await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                               .UpdateAsync(new { IsRead = true });

            if (mailItem.Count == 0)
            {
                return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.None, content, null);
            }

            return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.None, content, mailItem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[ReadMail] ErrorCode: {ErrorCode.ReadMailFailException}, MailId: {mailid}");
            return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.ReadMailFailException, null, null);
        }
    }

    // 메일 아이템 수령
    // Mail_Item 테이블에서 아이템 정보 가져와서 User_Item 테이블에 추가
    public async Task<Tuple<ErrorCode, List<UserItem>>> ReceiveMailItemAsync(Int64 mailid, Int64 userId)
    {
        try
        {
            var isCorrectRequest = await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                                              .Where("UserId", userId).Where("IsDeleted", false)
                                              .Select("UserId", "IsDeleted").ExistsAsync();

            if (isCorrectRequest == false)
            {
                _logger.ZLogError($"[ReceiveMailItem] ErrorCode: {ErrorCode.ReceiveMailItemFailWrongData}, MailId: {mailid}");
                return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.ReceiveMailItemFailWrongData, null);
            }

            var mailitem = await _queryFactory.Query("Mail_Item").Where("MailId", mailid)
                                              .GetAsync<MailItem>() as List<MailItem>;

            foreach (MailItem item in mailitem)
            {
                if (item.IsReceived == true)
                {
                    _logger.ZLogError($"[ReceiveMailItem] ErrorCode: {ErrorCode.ReceiveMailItemFailAlreadyGet}, ItemId: {item.ItemId}");
                    return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.ReceiveMailItemFailAlreadyGet, null);
                }
            }

            foreach (MailItem item in mailitem)
            {
                var errorCode = await InsertUserItemAsync(userId, item.ItemCode, item.ItemCount, item.ItemId);

                if (errorCode != ErrorCode.None)
                {
                    // 롤백
                    for (int i = 0; i <= mailitem.IndexOf(item); i++)
                    {
                        await DeleteUserItemAsync(userId, mailitem[i].ItemId);
                    }

                    _logger.ZLogError($"[ReceiveMailItem] ErrorCode: {ErrorCode.ReceiveMailItemFailInsertItem}, ItemId: {item.ItemId}");
                    return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.ReceiveMailItemFailInsertItem, null);
                }
            }

            await _queryFactory.Query("Mail_Item").Where("MailId", mailid)
                               .UpdateAsync(new { IsReceived = true });

            await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                               .UpdateAsync(new { HasItem = false });

            var userItem = await _queryFactory.Query("Mail_Item").Where("MailId", mailid)
                                              .GetAsync<UserItem>() as List<UserItem>;

            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.None, userItem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[ReceiveMailItem] ErrorCode: {ErrorCode.ReceiveMailItemFailException}, MailId: {mailid}");
            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.ReceiveMailItemFailException, null);
        }
    }

    // 메일 삭제
    // Mail_Data 테이블에서 논리적으로만 삭제
    public async Task<ErrorCode> DeleteMailAsync(Int64 mailid, Int64 userId)
    {
        try
        {
            var IsCorrectRequest = await _queryFactory.Query("Mail_Data").Where("MailId", mailid).Where("UserId", userId)
                                              .Where("IsDeleted", false).Select("UserId", "IsDeleted")
                                              .ExistsAsync();

            if (IsCorrectRequest == false)
            {
                _logger.ZLogError($"[DeleteMail] ErrorCode: {ErrorCode.DeleteMailFailWrongData}, MailId: {mailid}");
                return ErrorCode.DeleteMailFailWrongData;
            }
            else
            {
                await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                                   .UpdateAsync(new { IsDeleted = true });

                return ErrorCode.None;
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[DeleteMail] ErrorCode: {ErrorCode.DeleteMailFailException}, MailId: {mailid}");
            return ErrorCode.DeleteMailFailException;
        }
    }
}
