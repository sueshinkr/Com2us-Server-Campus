using System;
using Org.BouncyCastle.Asn1.Pkcs;
using SqlKata.Execution;
using WebAPIServer.DataClass;
using WebAPIServer.Util;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class GameDb : IGameDb
{
    // 유저 출석 데이터 로딩
    // User_Data 테이블에서 유저 출석 정보 가져온 뒤 새로운 출석인지 아닌지 판별
    public async Task<Tuple<ErrorCode, Int64, bool>> LoadAttendanceDataAsync(Int64 userId)
    {
        try
        {
            var userData = await _queryFactory.Query("User_Attendance").Where("UserId", userId)
                                              .FirstOrDefaultAsync<UserAttendance>();

            if (userData == null)
            {
                return new Tuple<ErrorCode, Int64, bool>(ErrorCode.LoadAttendanceDataFailWrongUser, 0, false);
            }

            if (userData.LastAttendance.Day == DateTime.Now.Day)
            {
                return new Tuple<ErrorCode, Int64, bool>(ErrorCode.None, userData.AttendanceCount, false);
            }
            else if (userData.AttendanceCount == 30 || userData.LastAttendance.Day + 1 < DateTime.Now.Day)
            {
                return new Tuple<ErrorCode, Int64, bool>(ErrorCode.None, 0, true);
            }

            return new Tuple<ErrorCode, Int64, bool>(ErrorCode.None, userData.AttendanceCount, true);
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.LoadAttendanceDataFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "LoadAttendanceData Exception");

            return new Tuple<ErrorCode, Int64, bool>(errorCode, 0, false);
        }
    }

    // 출석 처리
    // 새로운 출석에 대한 요청을 판별하여 처리, 보상 메일 전송
    public async Task<ErrorCode> HandleNewAttendanceAsync(Int64 userId)
    {
        try
        {
            var errorCode = new ErrorCode();
            var userData = await _queryFactory.Query("User_Attendance").Where("UserId", userId)
                                              .FirstOrDefaultAsync<UserAttendance>();

            if (userData == null)
            {
                return ErrorCode.HandleNewAttendanceFailWrongUser;
            }

            if (userData.LastAttendance.Day == DateTime.Now.Day)
            {                
                return ErrorCode.HandleNewAttendanceFailNotNewAttendance;
            }
            else if (userData.AttendanceCount == 30 || userData.LastAttendance.Day + 1 < DateTime.Now.Day)
            {
                await _queryFactory.Query("User_Attendance").Where("UserId", userId)
                                   .UpdateAsync(new
                                   {
                                       LastAttendance = DateTime.Now,
                                       AttendanceCount = 1,
                                   });

                errorCode = await SendMailAttendanceRewardAsync(userId, 1);
            }
            else
            {
                await _queryFactory.Query("User_Attendance").Where("UserId", userId)
                                   .UpdateAsync(new
                                   {
                                       LastAttendance = DateTime.Now,
                                       AttendanceCount = userData.AttendanceCount + 1
                                   });

                errorCode = await SendMailAttendanceRewardAsync(userId, userData.AttendanceCount + 1);
            }

            if (errorCode != ErrorCode.None)
            {
                // 롤백
                await _queryFactory.Query("User_Attendance").Where("UserId", userId)
                                   .UpdateAsync(new
                                   {
                                       LastAttendance = userData.LastAttendance,
                                       AttendanceCount = userData.AttendanceCount
                                   });

                return ErrorCode.HandleNewAttendanceFailSendMail;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            var errorCode = ErrorCode.HandleNewAttendanceFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "HandleNewAttendance Exception");

            return ErrorCode.HandleNewAttendanceFailException;
        }
    }

    // 출석 보상 메일 전송
    // Mail_data 및 Mail_Item 테이블에 데이터 추가
    private async Task<ErrorCode> SendMailAttendanceRewardAsync(Int64 userId, Int64 attendanceCount)
    {
        var mailId = _idGenerator.CreateId();

        try
        {
            var attendanceReward = _masterDb.AttendanceRewardInfo.Find(i => i.Code == attendanceCount);

            await _queryFactory.Query("Mail_Data").InsertAsync(new
            {
                MailId = mailId,
                UserId = userId,
                SenderId = 0,
                Title = $"{attendanceCount}일차 출석 보상 지급",
                Content = $"{attendanceCount}일차 출석 보상입니다.",
                hasItem = true,
                ExpiredAt = DateTime.Now.AddDays(7)
            });

            var errorCode = await InsertItemIntoMailAsync(mailId, attendanceReward.ItemCode, attendanceReward.Count);

            if (errorCode != ErrorCode.None)
            {
                // 롤백
                await _queryFactory.Query("Mail_Data").Where("MailId", mailId).DeleteAsync();
                await _queryFactory.Query("Mail_Item").Where("MailId", mailId).DeleteAsync();

                return ErrorCode.SendMailAttendanceRewardFailInsertItemIntoMail;
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            // 롤백
            await _queryFactory.Query("Mail_Data").Where("MailId", mailId).DeleteAsync();
            await _queryFactory.Query("Mail_Item").Where("MailId", mailId).DeleteAsync();

            var errorCode = ErrorCode.SendMailAttendanceRewardFailException;

            _logger.ZLogError(LogManager.MakeEventId(errorCode), ex, "SendMailAttendanceReward Exception");

            return errorCode;
        }
    }

}

