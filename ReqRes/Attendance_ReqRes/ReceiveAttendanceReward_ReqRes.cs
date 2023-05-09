﻿using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.RequestResponse;

public class ReceiveAttendanceRewardRequest
{
    public Int64 AccountId { get; set; }
    public string AuthToken { get; set; }
    public double AppVersion { get; set; }
    public double MasterVersion { get; set; }
    public Int64 UserId { get; set; }
    public Int64 attendanceCount { get; set; }
}

public class ReceiveAttendanceRewardResponse
{
    public ErrorCode Result { get; set; }
}