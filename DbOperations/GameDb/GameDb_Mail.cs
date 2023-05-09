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
    public async Task<Tuple<ErrorCode, List<MailData>>> LoadMailDataAsync(Int64 userId, Int64 pageNumber)
    {
        var mailData = new List<MailData>();
        var mailsPerPage = int.Parse(_configuration.GetSection("MailSetting")["mailsPerPage"]);

        if (pageNumber < 1)
        {
            _logger.ZLogError($"[LoadMailData] ErrorCode: {ErrorCode.LoadMailDataFailWrongPage}, UserId: {userId}, pageNumber: {pageNumber}");
            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.LoadMailDataFailWrongPage, mailData);
        }

        try
        {
            mailData = await _queryFactory.Query("Mail_Data").Where("UserId", userId).Where("IsDeleted", false)
                                          .OrderByDesc("ObtainedAt").Offset((pageNumber - 1) * mailsPerPage).Limit(mailsPerPage)
                                          .GetAsync<MailData>() as List<MailData>;

            if (mailData.Count == 0)
            {
                _logger.ZLogError($"[LoadMailData] ErrorCode: {ErrorCode.LoadMailDataFailNoData}, UserId: {userId}, pageNumber: {pageNumber}");
                return new Tuple<ErrorCode, List<MailData>>(ErrorCode.LoadMailDataFailNoData, mailData);
            }

            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.None, mailData);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[LoadMailData] ErrorCode: {ErrorCode.LoadMailDataFailException}, UserId: {userId}");
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
                                             .Where("UserId", userId).Where("IsDeleted", false).Select("Content")
                                             .FirstOrDefaultAsync<string>();

            if (content == null)
            {
                _logger.ZLogError($"[ReadMail] ErrorCode: {ErrorCode.ReadMailFailWrongData}, MailId: {mailId}");
                return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.ReadMailFailWrongData, null, null);
            }

            var mailItem = await _queryFactory.Query("Mail_Item").Where("MailId", mailId)
                                              .Select("ItemId", "ItemCode", "ItemCount", "IsReceived")
                                              .GetAsync<MailItem>() as List<MailItem>;

            await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                               .UpdateAsync(new { IsRead = true });

            if (mailItem.Count == 0)
            {
                return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.None, content, null);
            }

            return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.None, content, mailItem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[ReadMail] ErrorCode: {ErrorCode.ReadMailFailException}, MailId: {mailId}");
            return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.ReadMailFailException, null, null);
        }
    }

    // 메일 아이템 수령
    // Mail_Item 테이블에서 아이템 정보 가져와서 User_Item 테이블에 추가
    public async Task<Tuple<ErrorCode, List<UserItem>>> ReceiveMailItemAsync(Int64 mailId, Int64 userId)
    {
        try
        {
            var isCorrectRequest = await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                                              .Where("UserId", userId).Where("IsDeleted", false)
                                              .Select("UserId", "IsDeleted").ExistsAsync();

            if (isCorrectRequest == false)
            {
                _logger.ZLogError($"[ReceiveMailItem] ErrorCode: {ErrorCode.ReceiveMailItemFailWrongData}, MailId: {mailId}");
                return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.ReceiveMailItemFailWrongData, null);
            }

            var mailItem = await _queryFactory.Query("Mail_Item").Where("MailId", mailId)
                                              .GetAsync<MailItem>() as List<MailItem>;

            foreach (MailItem item in mailItem)
            {
                if (item.IsReceived == true)
                {
                    _logger.ZLogError($"[ReceiveMailItem] ErrorCode: {ErrorCode.ReceiveMailItemFailAlreadyGet}, ItemId: {item.ItemId}");
                    return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.ReceiveMailItemFailAlreadyGet, null);
                }
            }

            var userItem = new List<UserItem>();

            foreach (MailItem item in mailItem)
            {
                var errorCode = await InsertUserItemAsync(userId, item.ItemCode, item.ItemCount, item.ItemId);

                if (item.ItemCode != 1) // 돈 얻은거는 안알려줘도 되나...?
                {
                    userItem.Add(await _queryFactory.Query("User_Item").Where("ItemId", item.ItemId).FirstOrDefaultAsync<UserItem>());
                }

                if (errorCode != ErrorCode.None)
                {
                    // 롤백
                    for (int i = 0; i <= mailItem.IndexOf(item); i++)
                    {
                        await DeleteUserItemAsync(userId, mailItem[i].ItemId);
                    }

                    _logger.ZLogError($"[ReceiveMailItem] ErrorCode: {ErrorCode.ReceiveMailItemFailInsertItem}, ItemId: {item.ItemId}");
                    return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.ReceiveMailItemFailInsertItem, null);
                }
            }

            await _queryFactory.Query("Mail_Item").Where("MailId", mailId)
                               .UpdateAsync(new { IsReceived = true });

            await _queryFactory.Query("Mail_Data").Where("MailId", mailId)
                               .UpdateAsync(new { HasItem = false });

            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.None, userItem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[ReceiveMailItem] ErrorCode: {ErrorCode.ReceiveMailItemFailException}, MailId: {mailId}");
            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.ReceiveMailItemFailException, null);
        }
    }

    // 메일 삭제
    // Mail_Data 테이블에서 논리적으로만 삭제
    public async Task<ErrorCode> DeleteMailAsync(Int64 mailId, Int64 userId)
    {
        try
        {
            var isCorrectRequest = await _queryFactory.Query("Mail_Data").Where("MailId", mailId).Where("UserId", userId)
                                              .Where("IsDeleted", false).Select("UserId", "IsDeleted")
                                              .ExistsAsync();

            if (isCorrectRequest == false)
            {
                _logger.ZLogError($"[DeleteMail] ErrorCode: {ErrorCode.DeleteMailFailWrongData}, MailId: {mailId}");
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
            _logger.ZLogError(ex, $"[DeleteMail] ErrorCode: {ErrorCode.DeleteMailFailException}, MailId: {mailId}");
            return ErrorCode.DeleteMailFailException;
        }
    }
}
