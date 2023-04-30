using System;
using WebAPIServer.ModelDB;

namespace WebAPIServer.Services;

public interface IGameDb : IDisposable
{
    public Task<ErrorCode> CreateBasicDataAsync(Int64 accountid);
    public Task<Tuple<ErrorCode, UserData>> UserDataLoading(Int64 accountid);
    public Task<Tuple<ErrorCode, List<UserItem>>> UserItemLoading(Int64 accountid);
    public Task<Tuple<ErrorCode, List<MailData>>> MailDataLoadingAsync(Int64 userid, Int64 pagenumber);
}

