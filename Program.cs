using WebAPIServer.Services;
using WebAPIServer.Middleware;
using ZLogger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<IAccountDb, AccountDb>();
builder.Services.AddTransient<IGameDb, GameDb>();
builder.Services.AddSingleton<IRedisDb, RedisDb>();

builder.Services.AddSingleton<IMasterDb, MasterDb>();
builder.Services.AddControllers();

builder.Logging.ClearProviders();
builder.Logging.AddZLoggerConsole();

var app = builder.Build();

IConfiguration configuration = app.Configuration;

// 로그인 이후 유저 인증
app.UseMiddleware<WebAPIServer.Middleware.CheckUserAuth>();

app.UseRouting();
app.MapControllers();

app.Run(configuration["ServerAddress"]);