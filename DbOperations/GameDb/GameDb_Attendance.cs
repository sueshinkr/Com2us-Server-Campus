using System;
using SqlKata.Execution;
using WebAPIServer.DataClass;
using ZLogger;

namespace WebAPIServer.DbOperations;

public partial class GameDb : IGameDb
{
    // 유저 출석 데이터 로딩
    // User_Data 테이블에서 유저 출석 정보 가져오고 새로 출석시 보상 메일 전송
    public async Task<Tuple<ErrorCode, Int64, bool>> LoadAttendanceDataAsync(Int64 userId)
    {
        try
        {
            var userdata = await _queryFactory.Query("User_Data").Where("UserId", userId)
                                              .Select("AttendanceCount", "LastLogin", "LastAttendance")
                                              .FirstOrDefaultAsync<UserData>();

            if (userdata == null)
            {
                _logger.ZLogError($"[LoadAttendanceData] ErrorCode: {ErrorCode.LoadAttendanceDataFailWrongUser}, UserId: {userId}");
                return new Tuple<ErrorCode, Int64, bool>(ErrorCode.LoadAttendanceDataFailWrongUser, 0, false);
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
                                       AttendanceCount = attendanceCount,
                                       IsReceiveAttendanceReward = false
                                   });
            }
            else //if (userdata.LastAttendance.Day < DateTime.Now.Day)
            {
                attendanceCount += 1;

                await _queryFactory.Query("User_Data").Where("UserId", userId)
                                   .UpdateAsync(new
                                   {
                                       LastAttendance = DateTime.Now,
                                       AttendanceCount = attendanceCount,
                                       IsReceiveAttendanceReward = false
                                   });
            }

            return new Tuple<ErrorCode, Int64, bool>(ErrorCode.None, attendanceCount, true);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[LoadAttendanceData] ErrorCode: {ErrorCode.LoadAttendanceDataFailException}, UserId: {userId}");
            return new Tuple<ErrorCode, Int64, bool>(ErrorCode.LoadAttendanceDataFailException, 0, false);
        }
    }

    // 출석 보상 메일 전송
    // Mail_data 및 Mail_Item 테이블에 데이터 추가
    public async Task<ErrorCode> SendMailAttendanceRewardAsync(Int64 userId, Int64 attendanceCount)
    {
        var mailid = _idGenerator.CreateId();

        try
        {
            var checkNewAttendance = await _queryFactory.Query("User_Data").Where("UserId", userId).Select("IsReceiveAttendanceReward", "AttendanceCount").FirstOrDefaultAsync<UserData>();

            if (checkNewAttendance.IsReceiveAttendanceReward == true || checkNewAttendance.AttendanceCount != attendanceCount)
            {
                _logger.ZLogError($"[SendMailAttendanceReward] ErrorCode: {ErrorCode.SendMailAttendanceRewardFailAlreadyAttend}, UserId: {userId}");
                return ErrorCode.SendMailAttendanceRewardFailAlreadyAttend;
            }

            var attendanceReward = _masterDb.AttendanceRewardInfo.Find(i => i.Code == attendanceCount);

            await _queryFactory.Query("Mail_Data").InsertAsync(new
            {
                MailId = mailid,
                UserId = userId,
                SenderId = 0,
                Title = $"{attendanceCount}일차 출석 보상 지급",
                Content = $"{attendanceCount}일차 출석 보상입니다.",
                hasItem = true,
                ExpiredAt = DateTime.Now.AddDays(7)
            });

            var itemid = _idGenerator.CreateId();

            await _queryFactory.Query("Mail_Item").InsertAsync(new
            {
                ItemId = itemid,
                MailId = mailid,
                ItemCode = attendanceReward.ItemCode,
                ItemCount = attendanceReward.Count
            });

            await _queryFactory.Query("User_Data").Where("UserId", userId)
                               .UpdateAsync(new { IsReceiveAttendanceReward = true });

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            // 롤백
            await _queryFactory.Query("Mail_Data").Where("MailId", mailid).DeleteAsync();
            await _queryFactory.Query("Mail_Item").Where("MailId", mailid).DeleteAsync();

            _logger.ZLogError(ex, $"[SendMailAttendanceReward] ErrorCode: {ErrorCode.SendMailAttendanceRewardFailException}, UserId: {userId}");
            return ErrorCode.SendMailAttendanceRewardFailException;
        }
    }

}

