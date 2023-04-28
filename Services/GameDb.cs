using System.Data;
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

    public async Task<ErrorCode> CreateBasicData(Int64 accountid)
    {
        try
        {
            await _queryFactory.Query("UserData").InsertAsync(new
            {
                AccountId = accountid,
                Level = 1,
                Exp = 0,
                Money = 0,
                AttendanceCount = 1,
                ClearStage = 0
            });

            List<(Int64, Int64)> defaultitem = new List<(Int64, Int64)> { (2, 1), (4, 1), (5, 1), (6, 10) };
            foreach ((Int64 itemcode, Int64 count) in defaultitem)
            {
                var errorCode = await InsertItem(accountid, itemcode, count);
                if (errorCode != ErrorCode.None)
                {
                    return errorCode;
                }
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[CreateBasicData] ErrorCode: {ErrorCode.CreateBasicDataFailException}, AccountId: {accountid}");
            return ErrorCode.CreateBasicDataFailException;
        }
    }

    public async Task<ErrorCode> InsertItem(Int64 accountid, Int64 itemcode, Int64 count)
    {
        try
        {
            var item = _MasterDb.ItemInfo.Find(i => i.Code == itemcode);

            if (item.Type == "소모품")
            {
                string query = $"INSERT INTO UserItem_Consumable (AccountId, ItemCode, ItemCount) VALUES ({accountid}, {itemcode}, {count}) ON DUPLICATE KEY UPDATE ItemCount = ItemCount + {count}";
                await _queryFactory.StatementAsync(query);
            }
            else // 장비
            {
                await _queryFactory.Query("UserItem_Equipment").InsertAsync(new
                {
                    AccountId = accountid,
                    ItemCode = itemcode,
                    ObtainDate = DateTime.Now,
                    EnhanceCount = 0
                });
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[InsertItem] ErrorCode: {ErrorCode.InsertItemFailException}, AccountId: {accountid}, ItemCode: {itemcode}");
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
            _logger.ZLogError(ex, $"[DataLoading] ErrorCode: {ErrorCode.DataLoadingFailException}, AccountId: {accountid}");
            return new Tuple<ErrorCode, UserData>(ErrorCode.DataLoadingFailException, null);
        }
    }

    public async Task<Tuple<ErrorCode, UserItem>> UserItemLoading(Int64 accountid)
    {
        var useritem = new UserItem();

        try
        {
            useritem.Consumable = await _queryFactory.Query("UserItem_Consumable").Where("AccountId", accountid).GetAsync<UserItem_Consumable>() as List<UserItem_Consumable>;
            useritem.Equipment = await _queryFactory.Query("UserItem_Equipment").Where("AccountId", accountid).GetAsync<UserItem_Equipment>() as List<UserItem_Equipment>;

            return new Tuple<ErrorCode, UserItem>(ErrorCode.None, useritem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[ItemLoading] ErrorCode: {ErrorCode.ItemLoadingFailException}, AccountId: {accountid}");
            return new Tuple<ErrorCode, UserItem>(ErrorCode.ItemLoadingFailException, null);
        }
    }
}



/*
if (await _queryFactory.Query("UserItem").Where("ItemCode", itemcode).ExistsAsync())
{
    await _queryFactory.Query("UserItem").Where("ItemCode", itemcode).IncrementAsync("ItemCount", (int)count);
}
else
{
    await _queryFactory.Query("UserItem").InsertAsync(new
    {
        AccountId = accountid,
        ItemCode = itemcode,
        ItemCount = count,
        UniqueId = ""
    });
}
*/

//query = "SELECT LAST_INSERT_ID();";
//string query = "SELECT AUTO_INCREMENT FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'GameDb' AND TABLE_NAME = 'UserItem'";
//var uniqueid = await _queryFactory.StatementAsync(query);