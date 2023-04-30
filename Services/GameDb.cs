using System.Data;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MySqlConnector;
using SqlKata.Execution;
using WebAPIServer.ModelDB;
using ZLogger;

namespace WebAPIServer.Services;

public class GameDb : IGameDb
{
    readonly ILogger<GameDb> _logger;
    readonly IMasterDb _MasterDb;

    IDbConnection _dbConn;
    QueryFactory _queryFactory;

    public GameDb(ILogger<GameDb> logger, IConfiguration configuration, IMasterDb masterDb)
    {
        _logger = logger;
        _MasterDb = masterDb;

        var DbConnectString = configuration.GetSection("DBConnection")["GameDb"];
        _dbConn = new MySqlConnection(DbConnectString);

        var compiler = new SqlKata.Compilers.MySqlCompiler();
        _queryFactory = new SqlKata.Execution.QueryFactory(_dbConn, compiler);

        _logger.ZLogInformation("GameDb Connected");
    }

    public void Dispose()
    {
        _dbConn.Close();
        GC.SuppressFinalize(this);
    }

    public async Task<ErrorCode> CreateBasicDataAsync(Int64 accountid)
    {
        try
        {
            var userid = await _queryFactory.Query("UserData").InsertGetIdAsync<Int64>(new
            {
                AccountId = accountid,
                Level = 1,
                Exp = 0,
                Money = 0,
                AttendanceCount = 1,
                ClearStage = 0
            });

            List<(Int64, Int64)> defaultitem = new List<(Int64, Int64)> { (2, 1), (4, 1), (5, 1), (6, 401) };
            foreach ((Int64 itemcode, Int64 count) in defaultitem)
            {
                var errorCode = await InsertItem(userid, itemcode, count);
                if (errorCode != ErrorCode.None)
                {
                    return errorCode;
                }
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[CreateBasicDataAsync] ErrorCode: {ErrorCode.CreateBasicDataFailException}, AccountId: {accountid}");
            return ErrorCode.CreateBasicDataFailException;
        }
    }

    public async Task<ErrorCode> InsertItem(Int64 userid, Int64 itemcode, Int64 count)
    {
        try
        {
            var item = _MasterDb.ItemInfo.Find(i => i.Code == itemcode);

            if (item.Type == "소모품")
            {
                var max = 100;
                // db 자체에서 max 처리하는 방법도 있지 않을까?

                while (count > 0)
                {

                    var currentCount = await _queryFactory.Query("UserItem").Where("ItemCode", item.Code).Where("ItemCount", "<", 100).Select("ItemCount").FirstOrDefaultAsync<Int64>();
                    var insertcount = Math.Min(currentCount + count, max);

                    if (currentCount == 0)
                    {
                        await _queryFactory.Query("UserItem").InsertAsync(new
                        {
                            UserId = userid,
                            ItemCode = itemcode,
                            ItemCount = insertcount
                        });
                    }
                    else
                    {
                        _queryFactory.Query("UserItem").Where("ItemCode", item.Code).Where("ItemCount", "<", 100).Select("ItemCount").Update(new
                        {
                            Itemcount = insertcount
                        });
                    }

                    count -= insertcount;

                    if (count > 0)
                    {
                        insertcount = Math.Min(count, max);
                        await _queryFactory.Query("UserItem").InsertAsync(new
                        {
                            UserId = userid,
                            ItemCode = itemcode,
                            ItemCount = insertcount
                        });
                        count -= insertcount;
                    }
                }
            }
            else // 장비
            {
                await _queryFactory.Query("UserItem").InsertAsync(new
                {
                    UserId = userid,
                    ItemCode = itemcode,
                    ItemCount = count
                });
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[InsertItem] ErrorCode: {ErrorCode.InsertItemFailException}, UserId: {userid}, ItemCode: {itemcode}");
            return ErrorCode.InsertItemFailException;
        }
    }

    public async Task<Tuple<ErrorCode, UserData>> UserDataLoading(Int64 accountid)
    {
        try
        {
            var userdata = await _queryFactory.Query("UserData").Where("AccountId", accountid).FirstOrDefaultAsync<UserData>();
            return new Tuple<ErrorCode, UserData>(ErrorCode.None, userdata);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[DataLoading] ErrorCode: {ErrorCode.UserDataLoadingFailException}, AccountId: {accountid}");
            return new Tuple<ErrorCode, UserData>(ErrorCode.UserDataLoadingFailException, null);
        }
    }

    public async Task<Tuple<ErrorCode, List<UserItem>>> UserItemLoading(Int64 userid)
    {
        var useritem = new List<UserItem>();

        try
        {
            useritem = await _queryFactory.Query("UserItem").Where("UserId", userid).GetAsync<UserItem>() as List<UserItem>;

            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.None, useritem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[UserItemLoading] ErrorCode: {ErrorCode.UserItemLoadingFailException}, UserId: {userid}");
            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.UserItemLoadingFailException, null);
        }
    }

    public async Task<Tuple<ErrorCode, List<MailData>>> MailDataLoadingAsync(Int64 userid, Int64 pagenumber)
    {
        var maildata = new List<MailData>();

        try
        {
            maildata = await _queryFactory.Query("MailData").Where("UserId", userid).Offset((pagenumber - 1) * 20).Limit(20).GetAsync<MailData>() as List<MailData>;
            if (maildata.Count == 0)
            {
                _logger.ZLogError($"[MailDataLoading] ErrorCode: {ErrorCode.MailDataLoadingFailNoData}, UserId: {userid}, PageNumber: {pagenumber}");
                return new Tuple<ErrorCode, List<MailData>>(ErrorCode.MailDataLoadingFailNoData, maildata);
            }

            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.None, maildata);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[MailDataLoading] ErrorCode: {ErrorCode.MailDataLoadingFailException}, UserId: {userid}");
            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.MailDataLoadingFailException, maildata);
        }
    }
}