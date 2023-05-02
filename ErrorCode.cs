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

    MailDataLoadingFailWrongPage = 5001,
    MailDataLoadingFailNoData = 5002,
    MailDataLoadingFailException = 5003,
    MailReadingFailWrongUser = 5004,
    MailReadingFailDeletedMail = 5005,
    MailReadingFailException = 5006,
    MailItemReceivingFailWrongUser = 5007,
    MailItemReceivingFailDeletedMail = 5008,
    MailItemReceivingFailAlreadyGet = 5009,
    MailItemReceivingFailInsertItem = 5010,
    MailItemReceivingFailException = 5011,
    MailDeletingFailWrongUser = 5012,
    MailDeletingFailAlreadyDelete = 5013,
    MailDeletingFailException = 5014
}