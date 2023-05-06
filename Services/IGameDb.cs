using System;
using WebAPIServer.DataClass;

namespace WebAPIServer.Services;

public interface IGameDb : IDisposable
{
    public Task<ErrorCode> CreateBasicDataAsync(Int64 accountid);
    public Task<Tuple<ErrorCode, UserData>> UserDataLoading(Int64 accountid);
    public Task<Tuple<ErrorCode, List<UserItem>>> UserItemLoadingAsync(Int64 userid);

    public Task<Tuple<ErrorCode, List<MailData>>> MailDataLoadingAsync(Int64 userid, Int64 pagenumber);
    public Task<Tuple<ErrorCode, string, List<MailItem>>> MailReadingAsync(Int64 mailid, Int64 userid);
    public Task<Tuple<ErrorCode, List<UserItem>>> MailItemReceivingAsync(Int64 mailid, Int64 userid);
    public Task<ErrorCode> MailDeletingAsync(Int64 mailid, Int64 userid);

    public Task<Tuple<ErrorCode, Int64, bool>> AttendanceDataLoadingAsync(Int64 userid);

    public Task<ErrorCode> InAppPurchasingAsync(Int64 userId, Int64 purchaseId, Int64 productCode);

    public Task<Tuple<ErrorCode, UserItem>> ItemEnhancingAsync(Int64 userId, Int64 ItemId);

    public Task<Tuple<ErrorCode, List<Int64>>> StageListLoadingAsync(Int64 userId);
    public Task<Tuple<ErrorCode, List<Int64>, List<StageEnemy>>> StageSelectingAsync(Int64 userId, Int64 stageNum);
}

