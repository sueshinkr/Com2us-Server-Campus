using System.Data;
using IdGen;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SqlKata.Execution;
using WebAPIServer.DataClass;

using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class GameDb : IGameDb
{
    readonly ILogger<GameDb> _logger;
    readonly IMasterDb _masterDb;
    readonly IIdGenerator<long> _idGenerator;
    readonly IConfiguration _configuration;

    IDbConnection _dbConn;
    QueryFactory _queryFactory;

    public GameDb(ILogger<GameDb> logger, IMasterDb masterDb, IIdGenerator<long> idGenerator, IConfiguration configuration)
    {
        _logger = logger;
        _masterDb = masterDb;
        _idGenerator = idGenerator;
        _configuration = configuration;

        var DbConnectString = _configuration.GetSection("DBConnection")["GameDb"];
        _dbConn = new MySqlConnection(DbConnectString);

        var compiler = new SqlKata.Compilers.MySqlCompiler();
        _queryFactory = new SqlKata.Execution.QueryFactory(_dbConn, compiler);
    }

    public void Dispose()
    {
        _dbConn.Close();
        GC.SuppressFinalize(this);
    }

    // 유저 기본 데이터 생성
    // User_Data 테이블에 유저 추가 / User_Item 테이블에 아이템 추가
    public async Task<ErrorCode> CreateBasicDataAsync(Int64 accountId)
    {
        try
        {
            var userId = await _queryFactory.Query("User_Data")
                                            .InsertGetIdAsync<Int64>(new { accountId = accountId });

            await _queryFactory.Query("User_Attendance")
                               .InsertAsync(new { UserId = userId });

            List<(Int64, Int64)> defaultitem = new List<(Int64, Int64)> { (2, 1), (4, 1), (5, 1), (6, 100) };
            foreach ((Int64 itemCode, Int64 count) in defaultitem)
            {
                (var errorCode, var useritem) = await InsertUserItemAsync(userId, itemCode, count);
                if (errorCode != ErrorCode.None)
                {
                    // 롤백
                    await _queryFactory.Query("User_Data").Where("UserId", userId).DeleteAsync();
                    await _queryFactory.Query("User_Attendance").Where("UserId", userId).DeleteAsync();
                    await _queryFactory.Query("User_Item").Where("UserId", userId).DeleteAsync();

                    return errorCode;
                }
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "CreateBasicData Exception");

            return ErrorCode.CreateBasicDataFailException;
        }
    }

    // 유저 데이터에 아이템 추가
    // User_Item 테이블에 아이템 추가
    private async Task<Tuple<ErrorCode, ItemInfo>> InsertUserItemAsync(Int64 userId, Int64 itemCode, Int64 itemCount, Int64 itemId = 0)
    {
        var itemInfo = new ItemInfo();

        try
        {
            var itemData = _masterDb.ItemInfo.Find(i => i.Code == itemCode);

            if (itemData.Attribute == 5)
            {
                await _queryFactory.Query("User_Data").Where("UserId", userId)
                                   .IncrementAsync("Money", (int)itemCount);
            }
            else
            {
                if (itemId == 0)
                {
                    itemId = _idGenerator.CreateId();
                }

                await _queryFactory.Query("User_Item").InsertAsync(new
                {
                    ItemId = itemId,
                    UserId = userId,
                    ItemCode = itemCode,
                    ItemCount = itemCount,
                    Attack = itemData.Attack,
                    Defence = itemData.Defence,
                    Magic = itemData.Magic
                });
            }

            itemInfo.ItemCode = itemCode;
            itemInfo.ItemCount = itemCount;

            return new Tuple<ErrorCode, ItemInfo>(ErrorCode.None, itemInfo);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "InsertUserItem Exception");
            
            return new Tuple<ErrorCode, ItemInfo>(ErrorCode.InsertItemFailException, itemInfo);
        }
    }

    // 유저 데이터에서 아이템 제거
    // User_Item 테이블에서 아이템 제거
    private async Task<ErrorCode> DeleteUserItemAsync(Int64 userId, Int64 itemId, Int64 itemCount = 0)
    {
        try
        {
            Int64 isDeleted;

            if (itemId == 0)
            {
                isDeleted = await _queryFactory.Query("User_Data").Where("UserId", userId)
                                               .DecrementAsync("Money", (int)itemCount);
            }
            else
            {
                isDeleted = await _queryFactory.Query("User_Item").Where("UserId", userId)
                                               .Where("ItemId", itemId).DeleteAsync();
            }

            if (isDeleted == 0)
            {
                return ErrorCode.DeleteItemFailWrongData;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "DeleteUserItem Exception");
            
            return ErrorCode.DeleteItemFailException;
        }
    }

    // 유저 기본 데이터 로딩
    // User_Data 테이블에서 유저 기본 정보 가져오기
    public async Task<Tuple<ErrorCode, UserData>> UserDataLoading(Int64 accountId)
    {
        var userData = new UserData();

        try
        {
            userData = await _queryFactory.Query("User_Data").Where("accountId", accountId)
                                          .FirstOrDefaultAsync<UserData>();

            return new Tuple<ErrorCode, UserData>(ErrorCode.None, userData);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "DataLoading Exception");

            return new Tuple<ErrorCode, UserData>(ErrorCode.UserDataLoadingFailException, userData);
        }
    }

    // 유저 아이템 로딩
    // User_Item 테이블에서 유저 아이템 정보 가져오기
    public async Task<Tuple<ErrorCode, List<UserItem>>> UserItemLoadingAsync(Int64 userId)
    {
        var userItem = new List<UserItem>();

        try
        {
            userItem = await _queryFactory.Query("User_Item").Where("UserId", userId)
                                          .Where("IsDestroyed", false).GetAsync<UserItem>() as List<UserItem>;

            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.None, userItem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "UserItemLoadingAsync Exception");

            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.UserItemLoadingFailException, userItem);
        }
    } 
}