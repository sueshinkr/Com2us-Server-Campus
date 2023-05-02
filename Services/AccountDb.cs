using System.Data;
using WebAPIServer.Controllers;
using WebAPIServer.DataClass;
using MySqlConnector;
using SqlKata.Execution;
using ZLogger;
using IdGen;

namespace WebAPIServer.Services;

public class AccountDb : IAccountDb
{
    readonly ILogger<AccountDb> _logger;
    readonly IIdGenerator<long> _idGenerator;

    IDbConnection _dbConn;
    QueryFactory _queryFactory;

    public AccountDb(ILogger<AccountDb> logger, IIdGenerator<long> idGenerator, IConfiguration configuration)
    {
        _logger = logger;
        _idGenerator = idGenerator;

        var DbConnectString = configuration.GetSection("DBConnection")["AccountDb"];
        _dbConn = new MySqlConnection(DbConnectString);
        _dbConn.Open();

        var compiler = new SqlKata.Compilers.MySqlCompiler();
        _queryFactory = new SqlKata.Execution.QueryFactory(_dbConn, compiler);

        _logger.ZLogInformation("AccountDb Connected");
    }

    public void Dispose()
    {
        _dbConn.Close();
        GC.SuppressFinalize(this);
    }

    // 계정 정보 생성
    // Account 테이블에 계정 추가 
    public async Task<Tuple<ErrorCode, Int64>> CreateAccountAsync(string email, string password)
    {
        try
        {
            var accountid = _idGenerator.CreateId();
            var saltValue = Security.RandomString(64);
            var hashedPassword = Security.MakeHashedPassword(saltValue, password);
            
            await _queryFactory.Query("Account").InsertAsync(new
            {
                AccountId = accountid,
                Email = email,
                SaltValue = saltValue,
                HashedPassword = hashedPassword
            });

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

    // 계정 정보 검증
    // Account 테이블의 데이터를 바탕으로 검증
    public async Task<Tuple<ErrorCode, Int64>> VerifyAccountAsync(string email, string password)
    {
        try
        {
            var accountinfo = await _queryFactory.Query("Account").Where("Email", email).FirstOrDefaultAsync<Account>();
            if (accountinfo is null || accountinfo.AccountId == null)
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
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"[VerifyAccount] ErrorCode: {ErrorCode.VerifyAccountFailException}, Email: {email}");
            return new Tuple<ErrorCode, Int64>(ErrorCode.VerifyAccountFailException, 0);
        }
    }

}