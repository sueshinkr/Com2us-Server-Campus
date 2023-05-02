using System;
using WebAPIServer.DataClass;

namespace WebAPIServer.Services;

public interface IGameDb : IDisposable
{
    public Task<ErrorCode> CreateBasicDataAsync(Int64 accountid);
    public Task<Tuple<ErrorCode, UserData>> UserDataLoading(Int64 accountid);
    public Task<Tuple<ErrorCode, List<UserItem>>> UserItemLoading(Int64 userid);

    public Task<Tuple<ErrorCode, List<MailData>>> MailDataLoadingAsync(Int64 userid, Int64 pagenumber);
    public Task<Tuple<ErrorCode, string, List<MailItem>>> MailReadingAsync(Int64 mailid, Int64 userid);
    public Task<ErrorCode> MailItemReceivingAsync(Int64 mailid, Int64 userid);
    public Task<ErrorCode> MailDeletingAsync(Int64 mailid, Int64 userid);
}

