using System.Collections.Generic;
using System.Data;
using Google.Protobuf.Collections;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Runtime.Intrinsics.X86;
using System.Xml;
using MySqlConnector;
using SqlKata.Execution;
using WebAPIServer.DataClass;
using ZLogger;

namespace WebAPIServer.DbOperations;

public class MasterDb : IMasterDb
{
    // 데이터베이스에서 마스터데이터 가져오기
    // 이후에는 마스터데이터와 관련된 것들은 여기서 사용

    readonly ILogger<MasterDb> _logger;

    public VersionData VersionDataInfo { get; }
    public List<Item> ItemInfo { get; }
    public List<ItemAttribute> ItemAttributeInfo { get; }
    public List<AttendanceReward> AttendanceRewardInfo { get; }
    public List<InAppProduct> InAppProductInfo { get; }
    public List<StageItem> StageItemInfo { get; }
    public List<StageEnemy> StageEnemyInfo { get; }
    public List<ExpTable> ExpTableInfo { get; }

    IDbConnection _dbConn;
    QueryFactory _queryFactory;

    public MasterDb(ILogger<MasterDb> logger, IConfiguration configuration)
    {
        _logger = logger;

        var DbConnectString = configuration.GetSection("DBConnection")["MasterDataDb"];
        _dbConn = new MySqlConnection(DbConnectString);

        var compiler = new SqlKata.Compilers.MySqlCompiler();
        _queryFactory = new SqlKata.Execution.QueryFactory(_dbConn, compiler);

        _logger.ZLogInformation("MasterDb Connected");

        VersionDataInfo = _queryFactory.Query("VersionData").Select().FirstOrDefault<VersionData>();
        ItemInfo = _queryFactory.Query("Item").Select().Get<Item>() as List<Item>;
        ItemAttributeInfo = _queryFactory.Query("ItemAttribute").Select().Get<ItemAttribute>() as List<ItemAttribute>;
        AttendanceRewardInfo = _queryFactory.Query("AttendanceReward").Select().Get<AttendanceReward>() as List<AttendanceReward>;
        InAppProductInfo = _queryFactory.Query("InAppProduct").Select().Get<InAppProduct>() as List<InAppProduct>;
        StageItemInfo = _queryFactory.Query("StageItem").Select().Get<StageItem>() as List<StageItem>;
        StageEnemyInfo = _queryFactory.Query("StageEnemy").Select().Get<StageEnemy>() as List<StageEnemy>;
        ExpTableInfo = _queryFactory.Query("ExpTable").Select().Get<ExpTable>() as List<ExpTable>;

        _logger.ZLogInformation("MasterDb Loading Completed");

        _dbConn.Close();
    }

    // 게임 버전 검증
    // MasterData에서 가져온 데이터를 바탕으로 검증
    public ErrorCode VerifyVersionDataAsync(double appVersion, double masterVersion)
    {
        try
        {
            if (VersionDataInfo.AppVersion != appVersion || VersionDataInfo.MasterVersion != masterVersion)
            {
                return ErrorCode.LoginFailGameDataNotMatch;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[VerifyVersionData] ErrorCode: {ErrorCode.VerifyVersionDataFailException}");
            return ErrorCode.VerifyVersionDataFailException;
        }
    }
}
