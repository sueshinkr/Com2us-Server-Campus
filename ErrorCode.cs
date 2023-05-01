public enum ErrorCode : UInt16
{
    None = 0,
    GetVersionDataFailRedis = 1,
    GetVersionDataFailException = 2,

    CreateAccountFailDuplicate = 1001,
    CreateAccountFailException = 1002,
    CreateBasicDataFailInsertItem = 1003,
    CreateBasicDataFailException = 1004,

    LoginFailUserNotExist = 2001,
    LoginFailPwNotMatch = 2002,
    LoginFailRegistUser = 2003,
    LoginFailGameDataNotMatch = 2004,
    VerifyAccountFailException = 2005,
    VerifyVersionDataFailNoData = 2006,
    VerifyVersionDataFailException = 2007,
    RegistUserFailException = 2008,
    NotificationLoadingFailNoUrl = 2009,
    NotificationLoadingFailGetImageFromUrl = 2010,
    NotificationLoadingFailException = 2011,


    InsertItemFailException = 3001,
    UserDataLoadingFailException = 3002,
    UserItemLoadingFailException = 3003,

    EmptyRequestHttpBody = 4001,
    InvalidRequestHttpBody = 4002,
    CheckUserGameDataNotMatch = 4003,
    UserNotRegisted = 4004,
    AuthTokenFailWrongAuthToken = 4005,
    AuthTokenFailSetNx = 4006,
    SetJsonFailException = 4007,

    MailDataLoadingFailNoData = 5001,
    MailDataLoadingFailException = 5002,
    MailReadingFailException = 5003,
    MailItemReceivingFailAlreadyGet = 5004,
    MailItemReceivingFailInsertItem = 5005,
    MailItemReceivingFailException = 5006
}
