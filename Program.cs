using WebAPIServer.DbOperations;
using WebAPIServer.Middleware;
using WebAPIServer.Log;
using ZLogger;
using IdGen.DependencyInjection; //https://github.com/RobThree/IdGen
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<IAccountDb, AccountDb>();
builder.Services.AddTransient<IGameDb, GameDb>();
builder.Services.AddSingleton<IRedisDb, RedisDb>(); //getrequiredservice?
builder.Services.AddSingleton<IMasterDb, MasterDb>();
builder.Services.AddIdGen(1);

builder.Services.AddControllers();

LogManager.SetLogging(builder);

var app = builder.Build();

IConfiguration configuration = app.Configuration;

// 로그인 이후 유저 인증
app.UseMiddleware<WebAPIServer.Middleware.CheckUserAuth>();

app.UseRouting();
app.MapControllers();

app.Run(configuration["ServerAddress"]);
