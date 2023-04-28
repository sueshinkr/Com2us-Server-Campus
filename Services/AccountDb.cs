using System.Data;
using WebAPIServer.Controllers;
using WebAPIServer.ModelDB;
using MySqlConnector;
using SqlKata.Execution;
using ZLogger;
namespace WebAPIServer.Services;

public class AccountDb : IAccountDb
{
    readonly ILogger<AccountDb> _logger;

    IDbConnection _dbConn;
    QueryFactory _queryFactory;

    public AccountDb(ILogger<AccountDb> logger, IConfiguration configuration)
    {
        _logger = logger;

        var DbConnectString = configuration.GetSection("DBConnection")["AccountDb"];
        _dbConn = new MySqlConnection(DbConnectString);

        var compiler = new SqlKata.Compilers.MySqlCompiler();
        _queryFactory = new SqlKata.Execution.QueryFactory(_dbConn, compiler);

        _logger.ZLogInformation("AccountDb Connected");

    }

    public async Task<Tuple<ErrorCode, Int64>> CreateAccountAsync(string email, string password)
    {
        try
        {
            var saltValue = Security.RandomString(64);
            var hashedPassword = Security.MakeHashedPassword(saltValue, password);

            await _queryFactory.Query("account").InsertAsync(new
            {
                Email = email,
                SaltValue = saltValue,
                HashedPassword = hashedPassword
            });

            var accountid = await _queryFactory.Query("account").Where("Email", email).Select("AccountId").FirstOrDefaultAsync<Int64>();

            return new Tuple<ErrorCode, Int64>(ErrorCode.None, accountid);
        }
        catch (MySqlException ex)
        {
            if (ex.Number == 1062)
            {
                _logger.ZLogError(ex, $"[CreateAccount] ErrorCode: {ErrorCode.CreateAccountFailDuplicate}, Email: {email}, ErrorNum : {ex.Number}");
                return new Tuple<ErrorCode, Int64>(ErrorCode.CreateAccountFailDuplicate, 0);
            }
            else
            {
                _logger.ZLogError(ex, $"[CreateAccount] ErrorCode: {ErrorCode.CreateAccountFailException}, Email: {email}, ErrorNum : {ex.Number}");
                return new Tuple<ErrorCode, Int64>(ErrorCode.CreateAccountFailException, 0);
            }
        }
    }

    public async Task<Tuple<ErrorCode, Int64>> VerifyAccountAsync(string email, string password)
    {
        try
        {
            var accountinfo = await _queryFactory.Query("account").Where("Email", email).FirstOrDefaultAsync<Account>();
            if (accountinfo is null || accountinfo.AccountId == 0)
            {
                return new Tuple<ErrorCode, Int64>(ErrorCode.LoginFailUserNotExist, 0);
            }

            var hashedPassword = Security.MakeHashedPassword(accountinfo.SaltValue, password);
            if (hashedPassword != accountinfo.HashedPassword)
            {
                _logger.ZLogError($"[VerifyAccount] ErrorCode: {ErrorCode.LoginFailPwNotMatch}, Email: {email}");
                return new Tuple<ErrorCode, Int64>(ErrorCode.LoginFailPwNotMatch, 0);
            }

            return new Tuple<ErrorCode, Int64>(ErrorCode.None, accountinfo.AccountId);
        }
        catch (MySqlException ex)
        {
            _logger.ZLogError(ex, $"[VerifyAccount] ErrorCode: {ErrorCode.VerifyAccountFailException}, Email: {email}, ErrorNum : {ex.Number}");
            return new Tuple<ErrorCode, Int64>(ErrorCode.VerifyAccountFailException, 0);
        }
    }

}