using Ledger.Service;
using Microsoft.EntityFrameworkCore;
using MoneyMovement.Observability;
using MoneyMovement.Outbox;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("Ledger.Service");
builder.Services.AddHealthChecks().AddDefaultHealthCheck();

var conn = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=ledger;Username=postgres;Password=postgres";
builder.Services.AddDbContext<LedgerDbContext>(o => o.UseNpgsql(conn));
builder.Services.AddScoped<IOutboxStore, OutboxStore<LedgerDbContext>>();
builder.Services.AddOutboxPublisher(useInMemoryBus: true);

var tbAddress = builder.Configuration["TigerBeetle:Address"] ?? "3000";
builder.Services.AddSingleton<ITigerBeetleClient>(_ => new TigerBeetleClientImpl(tbAddress));
builder.Services.AddScoped<ILedgerService, LedgerServiceApp>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseObservability();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthEndpoints();
app.MapGet("/version", () => new { version = "1.0.0" });

await app.RunAsync();
