﻿using System;
using System.ComponentModel.DataAnnotations;
using WebAPIServer.DataClass;

namespace WebAPIServer.RequestResponse;

public class SelectStageRequest
{
    public Int64 AccountId { get; set; }
    public string AuthToken { get; set; }
    public double AppVersion { get; set; }
    public double MasterVersion { get; set; }
    public Int64 UserId { get; set; }
    public Int64 StageCode { get; set; }
}

public class SelectStageResponse
{
    public ErrorCode Result { get; set; }
    public List<Int64> stageItem { get; set; }
    public List<StageEnemy> stageEnemy { get; set; }
}