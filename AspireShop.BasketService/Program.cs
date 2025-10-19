using AspireShop.BasketService;
using AspireShop.BasketService.Repositories;
using AspireShop.BasketService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisClient("basketcache");

builder.Services.AddGrpc();
builder.Services.AddGrpcHealthChecks();
builder.Services.AddTransient<IBasketRepository, RedisBasketRepository>();
builder.Services.AddSingleton<IPaymentSessionGuard, PaymentSessionGuard>();

var app = builder.Build();

app.MapGrpcService<BasketService>();

app.MapGrpcHealthChecksService();

app.MapDefaultEndpoints();

app.Run();
