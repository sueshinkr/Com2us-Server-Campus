using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.RequestResponse;

public class OpenAttendanceRequest
{
    public Int64 AccountId { get; set; }
    public string AuthToken { get; set; }
    public double AppVersion { get; set; }
    public double MasterVersion { get; set; }
    public Int64 UserId { get; set; }
}

public class OpenAttendanceResponse
{
    public ErrorCode Result { get; set; }
    public List<AttendanceReward> attendanceReward { get; set; }
    public List<CheckAttendance> checkAttendance { get; set; }
    // 각 출석일수마다의 아이템 수령여부도 필요할...
}