public enum ErrorCode : UInt16
{
    None = 0,
    GetGameDataFailRedis = 1,
    GetGameDataFailException = 2,

    CreateAccountFailDuplicate = 1001,
    CreateAccountFailException = 1002,
    CreateBasicDataFailException = 1003,

    LoginFailUserNotExist = 2001,
    LoginFailPwNotMatch = 2002,
    LoginFailRegistUser = 2003,
    LoginFailGameDataNotMatch = 2004,
    VerifyAccountFailException = 2005,
    VerifyGameFailNoGameData = 2006,
    VerifyGameDataFailException = 2007,
    RegistUserFailException = 2007,

    InsertItemFailException = 3001,
    DataLoadingFailException = 3002,
    ItemLoadingFailException = 3002,

    EmptyRequestHttpBody = 4001,
    InvalidRequestHttpBody = 4002,
    CheckUserGameDataNotMatch = 4003,
    UserNotRegisted = 4004,
    AuthTokenFailWrongAuthToken = 4005,
    AuthTokenFailSetNx = 4006,
    SetJsonFailException = 4007
}
