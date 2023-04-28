using System;
using WebAPIServer.ModelDB;

namespace WebAPIServer.Services;

public interface IGameDb
{
    public Task<ErrorCode> CreateBasicData(Int64 accountid);
    public Task<Tuple<ErrorCode, UserData>> UserDataLoading(Int64 accountid);
    public Task<Tuple<ErrorCode, UserItem>> UserItemLoading(Int64 accountid);
}

