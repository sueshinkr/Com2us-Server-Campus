using System;
using WebAPIServer.DataClass;

namespace WebAPIServer.DbOperations;

public interface IGameDb : IDisposable
{
    public Task<ErrorCode> CreateBasicDataAsync(Int64 accountid);
    public Task<Tuple<ErrorCode, UserData>> UserDataLoading(Int64 accountid);
    public Task<Tuple<ErrorCode, List<UserItem>>> UserItemLoadingAsync(Int64 userid);

    public Task<Tuple<ErrorCode, List<MailData>>> LoadMailDataAsync(Int64 userid, Int64 pagenumber);
    public Task<Tuple<ErrorCode, string, List<MailItem>>> ReadMailAsync(Int64 mailid, Int64 userid);
    public Task<Tuple<ErrorCode, List<ItemInfo>>> ReceiveMailItemAsync(Int64 mailid, Int64 userid);
    public Task<ErrorCode> DeleteMailAsync(Int64 mailid, Int64 userid);

    public Task<Tuple<ErrorCode, Int64, bool>> LoadAttendanceDataAsync(Int64 userid);
    public Task<ErrorCode> HandleNewAttendanceAsync(Int64 userId);

    public Task<ErrorCode> PurchaseInAppProductAsync(Int64 userId, Int64 purchaseId, Int64 productCode);

    public Task<Tuple<ErrorCode, UserItem>> EnhanceItemAsync(Int64 userId, Int64 ItemId);

    public Task<Tuple<ErrorCode, List<ClearData>>> LoadStageListAsync(Int64 userId);
    public Task<Tuple<ErrorCode, List<StageItem>, List<StageEnemy>>> SelectStageAsync(Int64 userId, Int64 stageCode);
    public Task<Tuple<ErrorCode, List<ItemInfo>, Int64>> ReceiveStageClearRewardAsync(Int64 userId, Int64 stageCode, List<ItemInfo> itemList);
    public Task<ErrorCode> UpdateStageClearDataAsync(Int64 userId, Int64 stageCode, Int64 clearRank, TimeSpan clearTime);
}

