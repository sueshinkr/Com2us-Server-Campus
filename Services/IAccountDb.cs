using WebAPIServer.DataClass;

namespace WebAPIServer.Services;

public interface IAccountDb : IDisposable
{
    // 추후 기능 추가 예정
    public Task<Tuple<ErrorCode, Int64>> CreateAccountAsync(string email, string password);
    public Task<Tuple<ErrorCode, Int64>> VerifyAccountAsync(string email, string password);
}
