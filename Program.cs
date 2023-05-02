using WebAPIServer.Services;
using WebAPIServer.Middleware;
using ZLogger;
using IdGen.DependencyInjection; //https://github.com/RobThree/IdGen

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<IAccountDb, AccountDb>();
builder.Services.AddTransient<IGameDb, GameDb>();
builder.Services.AddSingleton<IRedisDb, RedisDb>(); //getrequiredservice?
builder.Services.AddSingleton<IMasterDb, MasterDb>();
builder.Services.AddIdGen(1);

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