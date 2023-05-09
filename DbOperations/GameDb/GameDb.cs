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
    readonly IMasterDb _MasterDb;
    readonly IIdGenerator<long> _idGenerator;
    readonly IConfiguration _configuration;

    IDbConnection _dbConn;
    QueryFactory _queryFactory;

    public GameDb(ILogger<GameDb> logger, IMasterDb masterDb, IIdGenerator<long> idGenerator, IConfiguration configuration)
    {
        _logger = logger;
        _MasterDb = masterDb;
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

            List<(Int64, Int64)> defaultitem = new List<(Int64, Int64)> { (2, 1), (4, 1), (5, 1), (6, 100) };
            foreach ((Int64 itemCode, Int64 count) in defaultitem)
            {
                var errorCode = await InsertUserItemAsync(userId, itemCode, count);
                if (errorCode != ErrorCode.None)
                {
                    // 롤백
                    await _queryFactory.Query("User_Data").Where("UserId", userId).DeleteAsync();
                    await _queryFactory.Query("User_Item").Where("UserId", userId).DeleteAsync();

                    _logger.ZLogError($"[CreateBasicDataAsync] ErrorCode: {ErrorCode.CreateBasicDataFailInsertItem}, accountId: {accountId}");
                    return errorCode;
                }
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[CreateBasicDataAsync] ErrorCode: {ErrorCode.CreateBasicDataFailException}, accountId: {accountId}");
            return ErrorCode.CreateBasicDataFailException;
        }
    }

    // 유저 데이터에 아이템 추가
    // User_Item 테이블에 아이템 추가
    private async Task<ErrorCode> InsertUserItemAsync(Int64 userId, Int64 itemCode, Int64 count, Int64 itemId = 0)
    {
        try
        {
            

            var item = _MasterDb.ItemInfo.Find(i => i.Code == itemCode);

            if (item.Attribute == 5)
            {
                await _queryFactory.Query("User_Data").Where("UserId", userId)
                                   .IncrementAsync("Money", (int)count);

                return ErrorCode.None;
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
                    ItemCount = count,
                    Attack = item.Attack,
                    Defence = item.Defence,
                    Magic = item.Magic
                });
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[InsertUserItem] ErrorCode: {ErrorCode.InsertItemFailException}, UserId: {userId}, ItemCode: {itemCode}");
            return ErrorCode.InsertItemFailException;
        }
    }

    // 유저 데이터에서 아이템 제거
    // User_Item 테이블에서 아이템 제거
    private async Task<ErrorCode> DeleteUserItemAsync(Int64 userId, Int64 itemId)
    {
        try
        {
            var isDeleted = await _queryFactory.Query("User_Item").Where("UserId", userId)
                                               .Where("ItemId", itemId).DeleteAsync();

            if (isDeleted == 0)
            {
                _logger.ZLogDebug($"[DeleteUserItem] ErrorCode: {ErrorCode.DeleteItemFailWrongData}, UserId: {userId}, ItemId: {itemId}");
                return ErrorCode.DeleteItemFailWrongData;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[DeleteUserItem] ErrorCode: {ErrorCode.DeleteItemFailException}, UserId: {userId}, ItemId: {itemId}");
            return ErrorCode.DeleteItemFailException;
        }
    }

    // 유저 기본 데이터 로딩
    // User_Data 테이블에서 유저 기본 정보 가져오기
    public async Task<Tuple<ErrorCode, UserData>> UserDataLoading(Int64 accountId)
    {
        try
        {
            var userData = await _queryFactory.Query("User_Data").Where("accountId", accountId).FirstOrDefaultAsync<UserData>();
            return new Tuple<ErrorCode, UserData>(ErrorCode.None, userData);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[DataLoading] ErrorCode: {ErrorCode.UserDataLoadingFailException}, accountId: {accountId}");
            return new Tuple<ErrorCode, UserData>(ErrorCode.UserDataLoadingFailException, null);
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
                                          .GetAsync<UserItem>() as List<UserItem>;

            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.None, userItem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[UserItemLoadingAsync] ErrorCode: {ErrorCode.UserItemLoadingFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.UserItemLoadingFailException, null);
        }
    } 
}