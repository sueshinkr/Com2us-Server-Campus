using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.RequestResponse;

public class OpenAttendanceSheetRequest
{
    public Int64 AccountId { get; set; }
    public string AuthToken { get; set; }
    public double AppVersion { get; set; }
    public double MasterVersion { get; set; }
    public Int64 UserId { get; set; }
}

public class OpenAttendanceSheetResponse
{
    public ErrorCode Result { get; set; }
    public List<AttendanceReward> attendanceReward { get; set; }
    public Int64 attendanceCount { get; set; }
    public bool IsNewAttendance { get; set; }
}