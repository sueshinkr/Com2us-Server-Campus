using WebAPIServer.DbOperations;
using WebAPIServer.Middleware;
using WebAPIServer.Log;
using ZLogger;
using IdGen.DependencyInjection; //https://github.com/RobThree/IdGen
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

var defaultSetting = new DefaultSetting();
configuration.Bind("DefaultSetting", defaultSetting);
builder.Services.AddSingleton(defaultSetting);

builder.Services.AddTransient<IAccountDb, AccountDb>();
builder.Services.AddTransient<IGameDb, GameDb>();
builder.Services.AddSingleton<IRedisDb, RedisDb>();
builder.Services.AddSingleton<IMasterDb, MasterDb>();
builder.Services.AddIdGen((int)defaultSetting.GeneratorId);

builder.Services.AddControllers();

LogManager.SetLogging(builder);

var app = builder.Build();

var redisDb = app.Services.GetRequiredService<IRedisDb>();
var redisDbTask = redisDb.Init();

var masterDb = app.Services.GetRequiredService<IMasterDb>();
var masterDbTask = masterDb.Init();

await Task.WhenAll(redisDbTask, masterDbTask);

// 로그인 이후 유저 인증
app.UseMiddleware<WebAPIServer.Middleware.CheckUserAuth>();

app.UseRouting();
app.MapControllers();

app.Run(configuration["ServerAddress"]);
