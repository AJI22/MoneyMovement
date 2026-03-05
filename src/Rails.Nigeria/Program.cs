using Microsoft.EntityFrameworkCore;
using MoneyMovement.Observability;
using MoneyMovement.Outbox;
using Rails.Nigeria;
using Rails.Nigeria.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("Rails.Nigeria");
builder.Services.AddHealthChecks().AddDefaultHealthCheck();

var conn = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=rails_ng;Username=postgres;Password=postgres";
builder.Services.AddDbContext<NigeriaRailDbContext>(o => o.UseNpgsql(conn));
builder.Services.AddScoped<IOutboxStore, OutboxStore<NigeriaRailDbContext>>();
builder.Services.AddOutboxPublisher(useInMemoryBus: true);

builder.Services.AddHttpClient<ILedgerClient, LedgerClient>(client =>
{
    var baseUrl = builder.Configuration["LedgerService:BaseUrl"] ?? "http://ledger-service:8080/";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddScoped<NgRailService>();

var simDown = builder.Configuration.GetValue<bool>("SimulateNgProviderDown", false);
var failureRate = builder.Configuration.GetValue<double>("SimulateTimeoutRate", 0.3);
if (simDown)
{
    builder.Services.AddSingleton<INigeriaPaymentProvider>(new FlakyDownProvider(failureRate, 2000));
    builder.Services.AddSingleton<INigeriaPaymentProvider>(new AlwaysSuccessProvider());
}
else
{
    builder.Services.AddSingleton<INigeriaPaymentProvider>(new AlwaysSuccessProvider());
    builder.Services.AddSingleton<INigeriaPaymentProvider>(new FlakyDownProvider(failureRate, 100));
}

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
