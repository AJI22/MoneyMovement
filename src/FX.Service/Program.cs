using FX.Service;
using FX.Service.Venues;
using Microsoft.EntityFrameworkCore;
using MoneyMovement.Observability;
using MoneyMovement.Outbox;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("FX.Service");
builder.Services.AddHealthChecks().AddDefaultHealthCheck();

var conn = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=fx;Username=postgres;Password=postgres";
builder.Services.AddDbContext<FxDbContext>(o => o.UseNpgsql(conn));
builder.Services.AddScoped<IOutboxStore, OutboxStore<FxDbContext>>();
builder.Services.AddOutboxPublisher(useInMemoryBus: true);

builder.Services.AddHttpClient<ILedgerClient, LedgerClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["LedgerService:BaseUrl"] ?? "http://ledger-service:8080/");
});
builder.Services.AddScoped<FxServiceApp>();

builder.Services.AddSingleton<IFxVenue>(new AlwaysSuccessVenue());
builder.Services.AddSingleton<IFxVenue>(new FlakyDownVenue(builder.Configuration.GetValue<double>("SimulateTimeoutRate", 0.2)));
if (builder.Configuration.GetValue<bool>("SimulateFxPartialFill", false))
    builder.Services.AddSingleton<IFxVenue>(new PartialFillVenue());

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
