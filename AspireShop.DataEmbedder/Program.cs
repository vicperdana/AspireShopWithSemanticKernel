
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();

builder.AddQdrantClient("qdrant");
builder.AddNpgsqlDataSource(connectionName: "postgresdb");

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();