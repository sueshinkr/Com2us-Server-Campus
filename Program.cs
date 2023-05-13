using WebAPIServer.DbOperations;
using WebAPIServer.Middleware;
using WebAPIServer.Log;
using ZLogger;
using IdGen.DependencyInjection; //https://github.com/RobThree/IdGen
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<IAccountDb, AccountDb>();
builder.Services.AddTransient<IGameDb, GameDb>();
builder.Services.AddSingleton<IRedisDb, RedisDb>();
builder.Services.AddSingleton<IMasterDb, MasterDb>();
builder.Services.AddIdGen(1); // 머신번호?도 설정에서 가져오도록... 

builder.Services.AddControllers();

LogManager.SetLogging(builder);

var app = builder.Build();

var redisDb = app.Services.GetRequiredService<IRedisDb>();
var redisDbTask = redisDb.Init();

var masterDb = app.Services.GetRequiredService<IMasterDb>();
var masterDbTask = masterDb.Init();

await Task.WhenAll(redisDbTask, masterDbTask);

IConfiguration configuration = app.Configuration;

// 로그인 이후 유저 인증
app.UseMiddleware<WebAPIServer.Middleware.CheckUserAuth>();

app.UseRouting();
app.MapControllers();

app.Run(configuration["ServerAddress"]);
