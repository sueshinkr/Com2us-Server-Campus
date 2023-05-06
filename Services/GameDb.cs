using System.Data;
using IdGen;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SqlKata.Execution;
using WebAPIServer.DataClass;
using ZLogger;
using static Humanizer.On;

namespace WebAPIServer.Services;

public class GameDb : IGameDb
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
            var userId = await _queryFactory.Query("User_Data")
                                            .InsertGetIdAsync<Int64>(new { AccountId = accountid });

            List<(Int64, Int64)> defaultitem = new List<(Int64, Int64)> { (2, 1), (4, 1), (5, 1), (6, 100) };
            foreach ((Int64 itemcode, Int64 count) in defaultitem)
            {
                var errorCode = await InsertUserItemAsync(userId, itemcode, count);
                if (errorCode != ErrorCode.None)
                {
                    // 롤백
                    await _queryFactory.Query("User_Data").Where("UserId", userId).DeleteAsync();
                    await _queryFactory.Query("User_Item").Where("UserId", userId).DeleteAsync();

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
    private async Task<ErrorCode> InsertUserItemAsync(Int64 userId, Int64 itemcode, Int64 count, Int64 itemid = 0)
    {
        try
        {
            if (itemcode == 1)
            {
                await _queryFactory.Query("User_Data").Where("UserId", userId)
                                   .IncrementAsync("Money", (int)count);

                return ErrorCode.None;
            }

            var item = _MasterDb.ItemInfo.Find(i => i.Code == itemcode);

            if (itemid == 0)
            {
                itemid = _idGenerator.CreateId();
            }

            await _queryFactory.Query("User_Item").InsertAsync(new
            {
                ItemId = itemid,
                UserId = userId,
                ItemCode = itemcode,
                ItemCount = count,
                Attack = item.Attack,
                Defence = item.Defence,
                Magic = item.Magic
            });

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[InsertUserItem] ErrorCode: {ErrorCode.InsertItemFailException}, UserId: {userId}, ItemCode: {itemcode}");
            return ErrorCode.InsertItemFailException;
        }
    }

    // 유저 데이터에서 아이템 제거
    // User_Item 테이블에서 아이템 제거
    private async Task<ErrorCode> DeleteUserItemAsync(Int64 userId, Int64 itemid)
    {
        try
        {
            var IsDeleted = await _queryFactory.Query("User_Item").Where("UserId", userId).Where("ItemId", itemid)
                               .DeleteAsync();

            if (IsDeleted == 0)
            {
                _logger.ZLogDebug($"[DeleteUserItem] ErrorCode: {ErrorCode.DeleteItemFailWrongData}, UserId: {userId}, ItemId: {itemid}");
                return ErrorCode.DeleteItemFailWrongData;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[DeleteUserItem] ErrorCode: {ErrorCode.DeleteItemFailException}, UserId: {userId}, ItemId: {itemid}");
            return ErrorCode.DeleteItemFailException;
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
    public async Task<Tuple<ErrorCode, List<UserItem>>> UserItemLoadingAsync(Int64 userId)
    {
        var useritem = new List<UserItem>();

        try
        {
            useritem = await _queryFactory.Query("User_Item").Where("UserId", userId)
                                          .GetAsync<UserItem>() as List<UserItem>;

            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.None, useritem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[UserItemLoadingAsync] ErrorCode: {ErrorCode.UserItemLoadingFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.UserItemLoadingFailException, null);
        }
    }

    // 메일 기본 데이터 로딩
    // Mail_Data 테이블에서 메일 기본 정보 가져오기
    public async Task<Tuple<ErrorCode, List<MailData>>> MailDataLoadingAsync(Int64 userId, Int64 pagenumber)
    {
        var maildata = new List<MailData>();
        var mailsperpage = int.Parse(_configuration.GetSection("MailSetting")["MailsPerPage"]);

        if (pagenumber < 1)
        {
            _logger.ZLogError($"[MailDataLoading] ErrorCode: {ErrorCode.MailDataLoadingFailWrongPage}, UserId: {userId}, PageNumber: {pagenumber}");
            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.MailDataLoadingFailWrongPage, maildata);
        }

        try
        {
            maildata = await _queryFactory.Query("Mail_Data").Where("UserId", userId).Where("IsDeleted", false)
                                          .OrderByDesc("ObtainedAt").Offset((pagenumber - 1) * mailsperpage).Limit(mailsperpage)
                                          .GetAsync<MailData>() as List<MailData>;

            if (maildata.Count == 0)
            {
                _logger.ZLogError($"[MailDataLoading] ErrorCode: {ErrorCode.MailDataLoadingFailNoData}, UserId: {userId}, PageNumber: {pagenumber}");
                return new Tuple<ErrorCode, List<MailData>>(ErrorCode.MailDataLoadingFailNoData, maildata);
            }

            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.None, maildata);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[MailDataLoading] ErrorCode: {ErrorCode.MailDataLoadingFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, List<MailData>>(ErrorCode.MailDataLoadingFailException, maildata);
        }
    }

    // 메일 본문 및 포함 아이템 읽기
    // Mail_Data 테이블에서 본문내용, Mail_Item 테이블에서 아이템 정보 가져오기
    public async Task<Tuple<ErrorCode, string, List<MailItem>>> MailReadingAsync(Int64 mailid, Int64 userId)
    {
        try
        {
            var content = await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                                             .Where("UserId", userId).Where("IsDeleted", false).Select("Content")
                                             .FirstOrDefaultAsync<string>();

            if (content == null)
            {
                _logger.ZLogError($"[MailReading] ErrorCode: {ErrorCode.MailReadingFailWrongData}, MailId: {mailid}");
                return new Tuple<ErrorCode, string, List<MailItem>>(ErrorCode.MailReadingFailWrongData, null, null);
            }

            var mailItem = await _queryFactory.Query("Mail_Item").Where("MailId", mailid)
                                              .Select("ItemId", "ItemCode", "ItemCount", "IsReceived")
                                              .GetAsync<MailItem>() as List<MailItem>;

            await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                               .UpdateAsync(new { IsRead = true });

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
    public async Task<Tuple<ErrorCode, List<UserItem>>> MailItemReceivingAsync(Int64 mailid, Int64 userId)
    {
        try
        {
            var isCorrectRequest = await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                                              .Where("UserId", userId).Where("IsDeleted", false)
                                              .Select("UserId", "IsDeleted").ExistsAsync();

            if (isCorrectRequest == false)
            {
                _logger.ZLogError($"[MailItemReceiving] ErrorCode: {ErrorCode.MailItemReceivingFailWrongData}, MailId: {mailid}");
                return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.MailItemReceivingFailWrongData, null);
            }

            var mailitem = await _queryFactory.Query("Mail_Item").Where("MailId", mailid)
                                              .GetAsync<MailItem>() as List<MailItem>;

            foreach (MailItem item in mailitem)
            {
                if (item.IsReceived == true)
                {
                    _logger.ZLogError($"[MailItemReceiving] ErrorCode: {ErrorCode.MailItemReceivingFailAlreadyGet}, ItemId: {item.ItemId}");
                    return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.MailItemReceivingFailAlreadyGet, null);
                }
            }

            foreach (MailItem item in mailitem)
            {
                var errorCode = await InsertUserItemAsync(userId, item.ItemCode, item.ItemCount, item.ItemId);

                if (errorCode != ErrorCode.None)
                {
                    // 롤백
                    for (int i = 0; i <= mailitem.IndexOf(item); i++)
                    {
                        await DeleteUserItemAsync(userId, mailitem[i].ItemId);
                    }

                    _logger.ZLogError($"[MailItemReceiving] ErrorCode: {ErrorCode.MailItemReceivingFailInsertItem}, ItemId: {item.ItemId}");
                    return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.MailItemReceivingFailInsertItem, null);
                }
            }

            await _queryFactory.Query("Mail_Item").Where("MailId", mailid)
                               .UpdateAsync(new { IsReceived = true });

            await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                               .UpdateAsync(new { HasItem = false });

            var userItem = await _queryFactory.Query("Mail_Item").Where("MailId", mailid)
                                              .GetAsync<UserItem>() as List<UserItem>;

            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.None, userItem);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[MailItemReceiving] ErrorCode: {ErrorCode.MailItemReceivingFailException}, MailId: {mailid}");
            return new Tuple<ErrorCode, List<UserItem>>(ErrorCode.MailItemReceivingFailException, null);
        }
    }

    // 메일 삭제
    // Mail_Data 테이블에서 논리적으로만 삭제
    public async Task<ErrorCode> MailDeletingAsync(Int64 mailid, Int64 userId)
    {
        try
        {
            var IsCorrectRequest = await _queryFactory.Query("Mail_Data").Where("MailId", mailid).Where("UserId", userId)
                                              .Where("IsDeleted", false).Select("UserId", "IsDeleted")
                                              .ExistsAsync();

            if (IsCorrectRequest == false)
            {
                _logger.ZLogError($"[MailDeleting] ErrorCode: {ErrorCode.MailDeletingFailWrongData}, MailId: {mailid}");
                return ErrorCode.MailDeletingFailWrongData;
            }
            else
            {
                await _queryFactory.Query("Mail_Data").Where("MailId", mailid)
                                   .UpdateAsync(new { IsDeleted = true });

                return ErrorCode.None;
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[MailDeleting] ErrorCode: {ErrorCode.MailDeletingFailException}, MailId: {mailid}");
            return ErrorCode.MailDeletingFailException;
        }
    }

    // 유저 출석 데이터 로딩
    // User_Data 테이블에서 유저 출석 정보 가져오고 새로 출석시 보상 메일 전송
    public async Task<Tuple<ErrorCode, Int64, bool>> AttendanceDataLoadingAsync(Int64 userId)
    {
        try
        {
            var userdata = await _queryFactory.Query("User_Data").Where("UserId", userId)
                                              .Select("AttendanceCount", "LastLogin", "LastAttendance")
                                              .FirstOrDefaultAsync<UserData>();

            if (userdata == null)
            {
                _logger.ZLogError($"[AttendanceDataLoading] ErrorCode: {ErrorCode.AttendanceDataLoadingFailWrongData}, UserId: {userId}");
                return new Tuple<ErrorCode, Int64, bool>(ErrorCode.AttendanceDataLoadingFailWrongData, 0, false);
            }

            var attendanceCount = userdata.AttendanceCount;

            if (userdata.LastAttendance.Day == DateTime.Now.Day)
            {
                return new Tuple<ErrorCode, Int64, bool>(ErrorCode.None, userdata.AttendanceCount, false);
            }
            else if (userdata.AttendanceCount == 30 || userdata.LastAttendance.Day + 1 < DateTime.Now.Day)
            {
                attendanceCount = 1;

                await _queryFactory.Query("User_Data").Where("UserId", userId)
                                   .UpdateAsync(new
                                   {
                                       LastAttendance = DateTime.Now,
                                       AttendanceCount = attendanceCount
                                   });
            }
            else //if (userdata.LastAttendance.Day < DateTime.Now.Day)
            {
                attendanceCount += 1;

                await _queryFactory.Query("User_Data").Where("UserId", userId)
                                   .UpdateAsync(new
                                   {
                                       LastAttendance = DateTime.Now,
                                       AttendanceCount = attendanceCount
                                   });
            }

            // 메일 전송
            var errorCode = await AttendanceRewardMailSending(userId, attendanceCount);
            if (errorCode != ErrorCode.None)
            {
                await _queryFactory.Query("User_Data").Where("UserId", userId)
                                   .UpdateAsync(new
                                   {
                                       LastAttendance = userdata.LastAttendance,
                                       AttendanceCount = userdata.AttendanceCount
                                   });

                return new Tuple<ErrorCode, Int64, bool>(errorCode, 0, false);
            }

            return new Tuple<ErrorCode, Int64, bool>(ErrorCode.None, attendanceCount, true);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[AttendanceDataLoading] ErrorCode: {ErrorCode.AttendanceDataLoadingFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, Int64, bool>(ErrorCode.AttendanceDataLoadingFailException, 0, false);
        }
    }

    // 출석 보상 메일 전송
    // Mail_data 및 Mail_Item 테이블에 데이터 추가
    public async Task<ErrorCode> AttendanceRewardMailSending(Int64 userId, Int64 attendancecount)
    {
        var mailid = _idGenerator.CreateId();

        try
        {
            var day = _MasterDb.AttendanceRewardInfo.Find(i => i.Code == attendancecount);

            await _queryFactory.Query("Mail_Data").InsertAsync(new
            {
                MailId = mailid,
                UserId = userId,
                SenderId = 0,
                Title = $"{attendancecount}일차 출석 보상 지급",
                Content = $"{attendancecount}일차 출석 보상입니다.",
                hasItem = true,
                ExpiredAt = DateTime.Now.AddDays(7)
            });

            var itemid = _idGenerator.CreateId();

            await _queryFactory.Query("Mail_Item").InsertAsync(new
            {
                ItemId = itemid,
                MailId = mailid,
                ItemCode = day.ItemCode,
                ItemCount = day.Count
            });

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            // 롤백
            await _queryFactory.Query("Mail_Data").Where("MailId", mailid).DeleteAsync();
            await _queryFactory.Query("Mail_Item").Where("MailId", mailid).DeleteAsync();

            _logger.ZLogError(ex, $"[AttendanceDataLoading] ErrorCode: {ErrorCode.AttendanceRewardMailSendingFailException}, UserId: {userId}");
            return ErrorCode.AttendanceRewardMailSendingFailException;
        }
    }

    // 인앱 결제 확인
    // InAppReceipt 테이블에 데이터 추가, 유저에게 상품 메일 전송
    public async Task<ErrorCode> InAppPurchasingAsync(Int64 userId, Int64 purchaseId, Int64 productCode)
    {
        try
        {
            await _queryFactory.Query("InAppReceipt").InsertAsync(new
            {
                PurchaseId = purchaseId,
                UserId = userId,
                ProductCode = productCode
            });

            // 메일 전송
            var errorCode = await InAppPurchaseMailSending(userId, productCode);
            if (errorCode != ErrorCode.None)
            {
                // 롤백
                await _queryFactory.Query("InAppReceipt").Where("PurchaseId", purchaseId)
                                   .DeleteAsync();

                return errorCode;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            if (ex is MySqlException mysqlEx && mysqlEx.Number == 1062)
            {
                _logger.ZLogError(mysqlEx, $"[InAppPurchasing] ErrorCode: {ErrorCode.InAppPurchasingFailDuplicate}, PurchaseId: {purchaseId}, ErrorNum : {mysqlEx.Number}");
                return ErrorCode.InAppPurchasingFailDuplicate;

            }
            else
            {
                _logger.ZLogError(ex, $"[InAppPurchasing] ErrorCode: {ErrorCode.InAppPurchasingFailException}, PurchaseId: {purchaseId}");
                return ErrorCode.InAppPurchasingFailException;
            }
        }
    }

    // 인앱 결제 상품 메일 전송
    // Mail_data 및 Mail_Item 테이블에 데이터 추가
    public async Task<ErrorCode> InAppPurchaseMailSending(Int64 userId, Int64 purchaseCode)
    {
        var mailid = _idGenerator.CreateId();

        try
        {
            await _queryFactory.Query("Mail_Data").InsertAsync(new
            {
                MailId = mailid,
                UserId = userId,
                SenderId = 0,
                Title = $"상품 {purchaseCode} 아이템 지급",
                Content = $"구매하신 상품 {purchaseCode}에 포함된 아이템을 지급해드립니다. 아이템 수령시 해당 결제에 대한 환불이 불가능함에 유의해주시기 바랍니다.",
                hasItem = true,
                ExpiredAt = DateTime.Now.AddYears(10)
            });

            foreach (InAppProduct product in _MasterDb.InAppProductInfo.FindAll(i => i.Code == purchaseCode))
            {
                var itemid = _idGenerator.CreateId();

                await _queryFactory.Query("Mail_Item").InsertAsync(new
                {
                    ItemId = itemid,
                    MailId = mailid,
                    ItemCode = product.ItemCode,
                    ItemCount = product.ItemCount
                });
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            // 롤백
            await _queryFactory.Query("Mail_Data").Where("MailId", mailid).DeleteAsync();
            await _queryFactory.Query("Mail_Item").Where("MailId", mailid).DeleteAsync();

            _logger.ZLogError(ex, $"[InAppPurchasing] ErrorCode: {ErrorCode.InAppPurchasingMailSendingFailException}, UserId: {userId}");
            return ErrorCode.InAppPurchasingMailSendingFailException;
        }
    }

    // 아이템 강화
    // User_Item 테이블 업데이트 및 User_Item_EnhanceHistory 테이블에 데이터 추가
    public async Task<Tuple<ErrorCode, UserItem>> ItemEnhancingAsync(Int64 userId, Int64 itemId)
    {
        UserItem itemData = new UserItem();
        var enhancedAt = DateTime.Now;

        try
        {
            itemData = await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                          .Where("UserId", userId).Where("IsDestroyed", false)
                                          .FirstOrDefaultAsync<UserItem>();

            if (itemData == null)
            {
                _logger.ZLogError($"[ItemEnhancing] ErrorCode: {ErrorCode.ItemEnhancingFailWrongData}, ItemId: {itemId}");
                return new Tuple<ErrorCode, UserItem>(ErrorCode.ItemEnhancingFailWrongData, null);
            }

            var enhanceData = _MasterDb.ItemInfo.Find(i => i.Code == itemData.ItemCode);

            if (enhanceData.EnhanceMaxCount == 0)
            {
                _logger.ZLogError($"[ItemEnhancing] ErrorCode: {ErrorCode.ItemEnhancingFailNotEnhanceable}, ItemId: {itemId}");
                return new Tuple<ErrorCode, UserItem>(ErrorCode.ItemEnhancingFailNotEnhanceable, null);
            }
            else if (itemData.EnhanceCount == enhanceData.EnhanceMaxCount)
            {
                _logger.ZLogError($"[ItemEnhancing] ErrorCode: {ErrorCode.ItemEnhancingFailAlreadyMax}, ItemId: {itemId}");
                return new Tuple<ErrorCode, UserItem>(ErrorCode.ItemEnhancingFailAlreadyMax, null);
            }

            var hasEnoughMoney = await _queryFactory.Query("User_Data").Where("UserId", userId)
                                        .Where("Money", ">", (itemData.EnhanceCount + 1) * 10)
                                        .DecrementAsync("Money", (int)(itemData.EnhanceCount + 1) * 10);

            if (hasEnoughMoney == 0)
            {
                _logger.ZLogError($"[ItemEnhancing] ErrorCode: {ErrorCode.ItemEnhancingFailNotEnoughMoney}, ItemId: {itemId}");
                return new Tuple<ErrorCode, UserItem>(ErrorCode.ItemEnhancingFailNotEnoughMoney, null);
            }

            var random = new Random();
            var isSuccess = random.NextDouble() < 0.85;

            if (isSuccess == true)
            {
                if (enhanceData.Attribute == 1) // 무기
                {
                    await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                       .UpdateAsync(new
                                       {
                                           EnhanceCount = itemData.EnhanceCount + 1,
                                           Attack = (Int64)Math.Round(itemData.Attack * 1.1)
                                       });
                }
                else if (enhanceData.Attribute == 2) // 방어구
                {
                    await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                       .UpdateAsync(new
                                       {
                                           EnhanceCount = itemData.EnhanceCount + 1,
                                           Defence = (Int64)Math.Round(itemData.Defence * 1.1)
                                       });
                }

                await _queryFactory.Query("User_Item_EnhanceHistory")
                                   .InsertAsync(new
                                   {
                                       ItemId = itemId,
                                       EnhanceCount_Before = itemData.EnhanceCount,
                                       IsSuccess = true,
                                       EnhancedAt = enhancedAt
                                   });

                itemData.EnhanceCount++;
                return new Tuple<ErrorCode, UserItem>(ErrorCode.None, itemData);
            }
            else
            {
                await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                                   .UpdateAsync(new { IsDestroyed = true });

                await _queryFactory.Query("User_Item_EnhanceHistory")
                                   .InsertAsync(new
                                   {
                                       ItemId = itemId,
                                       EnhanceCount_Before = itemData.EnhanceCount,
                                       IsSuccess = false,
                                       EnhancedAt = enhancedAt
                                   });

                itemData.IsDestoryed = true;
                return new Tuple<ErrorCode, UserItem>(ErrorCode.None, itemData);
            }
        }
        catch (Exception ex)
        {
            // 롤백
            await _queryFactory.Query("User_Item").Where("ItemId", itemId)
                               .UpdateAsync(new
                               {
                                   Attack = itemData.Attack,
                                   Defence = itemData.Defence,
                                   EnhanceCount = itemData.EnhanceCount,
                                   IsDestroyed = itemData.IsDestoryed
                               });

            await _queryFactory.Query("User_Item_EnhanceHistory").Where("ItemId", itemId).Where("EnhancedAt", enhancedAt).DeleteAsync();

            _logger.ZLogError(ex, $"[ItemEnhancing] ErrorCode: {ErrorCode.ItemEnhancingFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, UserItem>(ErrorCode.ItemEnhancingFailException, null);
        }
    }

    // 던전 스테이지 로딩
    // ClearStage 테이블에서 클리어한 스테이지 정보 가져오기 
    public async Task<Tuple<ErrorCode, List<Int64>>> StageListLoadingAsync(Int64 userId)
    {
        try
        {
            var clearStage = await _queryFactory.Query("ClearStage").Where("UserId", userId)
                                                .Select("StageCode").FirstOrDefaultAsync<List<Int64>>();

            return new Tuple<ErrorCode, List<Int64>>(ErrorCode.None, clearStage);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[StageLoading] ErrorCode: {ErrorCode.StageLoadingFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, List<Int64>>(ErrorCode.StageLoadingFailException, null);
        }
    }

    // 선택한 스테이지 검증
    // ClearStage 테이블에서 이전 스테이지 클리어 여부 확인하고 MasterData에서 스테이지 정보 가져오기
    public async Task<Tuple<ErrorCode, List<Int64>, List<StageEnemy>>> StageSelectingAsync(Int64 userId, Int64 stageNum)
    {
        try
        {
            var hasQualified = await _queryFactory.Query("ClearStage").Where("UserId", userId)
                                                .Where("StageCode", stageNum - 1).ExistsAsync();

            if (hasQualified == false)
            {
                _logger.ZLogError($"[StageSelecting] ErrorCode: {ErrorCode.StageSelectingFailNotQualified}, UserId: {userId}, StageNum : {stageNum}");
                return new Tuple<ErrorCode, List<Int64>, List<StageEnemy>>(ErrorCode.StageSelectingFailNotQualified, null, null);
            }

            var stageItem = _MasterDb.StageItemInfo.FindAll(i => i.Code == stageNum).Select(i => i.ItemCode).ToList();
            var stageEnemy = _MasterDb.StageEnemyInfo.FindAll(i => i.Code == stageNum);

            return new Tuple<ErrorCode, List<Int64>, List<StageEnemy>>(ErrorCode.None, stageItem, stageEnemy);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[StageLoading] ErrorCode: {ErrorCode.StageSelectingFailException}, UserId: {userId}, StageNum : {stageNum}");
            return new Tuple<ErrorCode, List<Int64>, List<StageEnemy>>(ErrorCode.StageSelectingFailException, null, null);
        }
    }

    // 던전 아이템 획득
    // 
}