using System.Data;
using IdGen;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MySqlConnector;
using SqlKata.Execution;
using WebAPIServer.DataClass;
using ZLogger;

namespace WebAPIServer.Services;

public class GameDb : IGameDb
{
    readonly ILogger<GameDb> _logger;
    readonly IMasterDb _MasterDb;
    readonly IIdGenerator<long> _idGenerator;

    IDbConnection _dbConn;
    QueryFactory _queryFactory;

    public GameDb(ILogger<GameDb> logger, IIdGenerator<long> idGenerator, IConfiguration configuration, IMasterDb masterDb)
    {
        _logger = logger;
        _MasterDb = masterDb;
        _idGenerator = idGenerator;

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

    // 유저 기본 데이터 생성
    // User_Data 테이블에 유저 추가 / User_Item 테이블에 아이템 추가
    public async Task<ErrorCode> CreateBasicDataAsync(Int64 accountid)
    {
        try
        {
            var userid = await _queryFactory.Query("User_Data").InsertGetIdAsync<Int64>(new
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
                    _logger.ZLogError($"[CreateBasicDataAsync] ErrorCode: {ErrorCode.CreateBasicDataFailInsertItem}, AccountId: {accountid}");
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

    // 유저 데이터에 아이템 추가
    // User_Item 테이블에 아이템 추가
    public async Task<ErrorCode> InsertItem(Int64 userid, Int64 itemcode, Int64 count, Int64 itemid = 0)
    {
        try
        {
            var item = _MasterDb.ItemInfo.Find(i => i.Code == itemcode);

            if (item.Attribute == 4)
            {
                var maxCount = 100;

                if (count > 0)
                {
                    var itemdata = await _queryFactory.Query("User_Item").Where("ItemCode", item.Code).Where("ItemCount", "<", 100).GetAsync<UserItem>() as UserItem;
                    var currentCount = itemdata?.ItemCount ?? 0;
                    var insertcount = Math.Min(currentCount + count, maxCount);

                    if (currentCount == 0)
                    {
                        if (itemid == 0)
                        {
                            itemid = _idGenerator.CreateId();
                        }

                        await _queryFactory.Query("User_Item").InsertAsync(new
                        {
                            ItemId = itemid,
                            UserId = userid,
                            ItemCode = itemcode,
                            ItemCount = insertcount
                        });
                    }
                    else
                    {
                        if (itemid == 0)
                        {
                            itemid = _idGenerator.CreateId();
                        }

                        await _queryFactory.Query("User_Item").Where("ItemId", itemid).UpdateAsync(new
                        {
                            Itemcount = insertcount
                        });
                    }

                    count -= insertcount;

                    while (count > 0)
                    {
                        if (itemid == 0)
                        {
                            itemid = _idGenerator.CreateId();
                        }

                        insertcount = Math.Min(count, maxCount);

                        await _queryFactory.Query("User_Item").InsertAsync(new
                        {
                            ItemId = itemid,
                            UserId = userid,
                            ItemCode = itemcode,
                            ItemCount = insertcount
                        });
                        count -= insertcount;
                    }
                }
            }
            else if (item.Attribute == 1 || item.Attribute == 2 || item.Attribute == 3)
            {
                if (itemid == 0)
                {
                    itemid = _idGenerator.CreateId();
                }

                await _queryFactory.Query("User_Item").InsertAsync(new
                {
                    Itemid = itemid,
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

    // 유저 기본 데이터 로딩
    // User_Data 테이블에서 유저 기본 정보 가져오기
    public async Task<Tuple<ErrorCode, UserData>> UserDataLoading(Int64 accountid)
    {
        try
        {
            var userdata = await _queryFactory.Query("User_Data").Where("AccountId", accountid).FirstOrDefaultAsync<UserData>();
            return new Tuple<ErrorCode, UserData>(ErrorCode.None, userdata);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[DataLoading] ErrorCode: {ErrorCode.UserDataLoadingFailException}, AccountId: {accountid}");
            return new Tuple<ErrorCode, UserData>(ErrorCode.UserDataLoadingFailException, null);
        }
    }

    // 유저 아이템 로딩
    // User_Item 테이블에서 유저 아이템 정보 가져오기
    public async Task<Tuple<ErrorCode, List<UserItem>>> UserItemLoading(Int64 userid)
    {
        var useritem = new List<UserItem>();

        try
        {
            useritem = await _queryFactory.Query("User_Item").Where("UserId", userid).GetAsync<UserItem>() as List<UserItem>;

            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.None, useritem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[UserItemLoading] ErrorCode: {ErrorCode.UserItemLoadingFailException}, UserId: {userid}");
            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.UserItemLoadingFailException, null);
        }
    }

    // 메일 기본 데이터 로딩
    // Mail_Data 테이블에서 메일 기본 정보 가져오기
    public async Task<Tuple<ErrorCode, List<MailData>>> MailDataLoadingAsync(Int64 userid, Int64 pagenumber)
    {
        var maildata = new List<MailData>();

        if (pagenumber < 1)
        {
            _logger.ZLogError($"[MailDataLoading] ErrorCode: {ErrorCode.MailDataLoadingFailWrongPage}, UserId: {userid}, PageNumber: {pagenumber}");
            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.MailDataLoadingFailWrongPage, maildata);
        }

        try
        {
            maildata = await _queryFactory.Query("Mail_Data").Where("UserId", userid).Where("IsDelete", false)
                                          .OrderByDesc("ObtainedAt").Offset((pagenumber - 1) * 20).Limit(20)
                                          .GetAsync<MailData>() as List<MailData>;
            // 한번에 읽어오는 갯수를 config에서 가져오는게 더 좋을듯
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

    // 메일 본문 및 포함 아이템 읽기
    // Mail_Data 테이블에서 본문내용, Mail_Item 테이블에서 아이템 정보 가져오기
    public async Task<Tuple<ErrorCode, string, List<MailItem>>> MailReadingAsync(Int64 mailid, Int64 userid)
    {
        try
        {
            var content = await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                                             .Where("UserId", userid).Where("IsDelete", false)
                                             .Select("Content").FirstOrDefaultAsync<string>();

            if (content == null)
            {
                _logger.ZLogError($"[MailReading] ErrorCode: {ErrorCode.MailReadingFailWrongData}, MailId: {mailid}");
                return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.MailReadingFailWrongData, null, null);
            }
            
            var mailItem = await _queryFactory.Query("Mail_Item").Where("MailId", mailid)
                                              .GetAsync<MailItem>() as List<MailItem>;

            await _queryFactory.Query("Mail_Data").Where("MailId", mailid).UpdateAsync(new
            {
                IsRead = true
            });

            if (mailItem.Count == 0)
            {
                return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.None, content, null);
            }

            return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.None, content, mailItem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[MailReading] ErrorCode: {ErrorCode.MailReadingFailException}, MailId: {mailid}");
            return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.MailReadingFailException, null, null);
        }
    }

    // 메일 아이템 수령
    // Mail_Item 테이블에서 아이템 정보 가져와서 User_Item 테이블에 추가
    public async Task<ErrorCode> MailItemReceivingAsync(Int64 mailid, Int64 userid)
    {
        // 한 메일 내의 아이템 일괄수령으로 변경
        try
        {
            var isCorrectRequest= await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                                              .Where("UserId", userid).Where("IsDelete", false)
                                              .Select("UserId", "IsDelete").ExistsAsync();

            if (isCorrectRequest == false)
            {
                _logger.ZLogError($"[MailItemReceiving] ErrorCode: {ErrorCode.MailItemReceivingFailWrongData}, MailId: {mailid}");
                return ErrorCode.MailItemReceivingFailWrongData;
            }

            var mailitem = await _queryFactory.Query("Mail_Item").Where("MailId", mailid)
                                              .GetAsync<MailItem>() as List<MailItem>;

            foreach (MailItem item in mailitem)
            {
                if (item.IsReveived == true)
                {
                    _logger.ZLogError($"[MailItemReceiving] ErrorCode: {ErrorCode.MailItemReceivingFailAlreadyGet}, ItemId: {item.ItemId}");
                    return ErrorCode.MailItemReceivingFailAlreadyGet;
                }
            }

            foreach (MailItem item in mailitem)
            {
                var errorCode = await InsertItem(userid, item.ItemCode, item.ItemCount, item.ItemId);
                if (errorCode != ErrorCode.None)
                {
                    _logger.ZLogError($"[MailItemReceiving] ErrorCode: {ErrorCode.MailItemReceivingFailInsertItem}, ItemId: {item.ItemId}");
                    return ErrorCode.MailItemReceivingFailInsertItem;
                }

                await _queryFactory.Query("Mail_Item").Where("ItemId", item.ItemId).UpdateAsync(new
                {
                    IsReveived = true
                });
            }
           
            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[MailItemReceiving] ErrorCode: {ErrorCode.MailItemReceivingFailException}, MailId: {mailid}");
            return ErrorCode.MailItemReceivingFailException;
        }
    }

    // 메일 삭제
    // Mail_Data 테이블에서 논리적으로만 삭제
    public async Task<ErrorCode> MailDeletingAsync(Int64 mailid, Int64 userid)
    {
        try
        {
            var maildata = await _queryFactory.Query("Mail_Data").Where("MailId", mailid).Select("UserId", "IsDelete").FirstAsync<MailData>();
            if (maildata.UserId != userid)
            {
                _logger.ZLogError($"[MailDeleting] ErrorCode: {ErrorCode.MailDeletingFailWrongUser}, MailId: {mailid}");
                return ErrorCode.MailDeletingFailWrongUser;
            }
            if (maildata.IsDelete == true)
            {
                _logger.ZLogError($"[MailDeleting] ErrorCode: {ErrorCode.MailDeletingFailAlreadyDelete}, MailId: {mailid}");
                return ErrorCode.MailDeletingFailAlreadyDelete;
            }
            else
            {
                await _queryFactory.Query("Mail_Data").Where("MailId", mailid).UpdateAsync(new
                {
                    IsDelete = true
                });

                return ErrorCode.None;
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[MailDeleting] ErrorCode: {ErrorCode.MailDeletingFailException}, MailId: {mailid}");
            return ErrorCode.MailDeletingFailException;
        }
    }
}