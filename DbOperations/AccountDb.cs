using System.Data;
using WebAPIServer.Controllers;
using WebAPIServer.DataClass;
using MySqlConnector;
using SqlKata.Execution;
using ZLogger;
using IdGen;
using WebAPIServer.Log;

namespace WebAPIServer.DbOperations;

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

        var compiler = new SqlKata.Compilers.MySqlCompiler();
        _queryFactory = new SqlKata.Execution.QueryFactory(_dbConn, compiler);
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
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "CreateAccount Exception");

            if (ex is MySqlException mysqlEx && mysqlEx.Number == 1062)
            {
                return new Tuple<ErrorCode, Int64>(ErrorCode.CreateAccountFailDuplicate, 0);
            }
            else
            {
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
                return new Tuple<ErrorCode, Int64>(ErrorCode.LoginFailPwNotMatch, 0);
            }

            return new Tuple<ErrorCode, Int64>(ErrorCode.None, accountinfo.AccountId);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "VerifyAccount Exception");

            return new Tuple<ErrorCode, Int64>(ErrorCode.VerifyAccountFailException, 0);
        }
    }

}