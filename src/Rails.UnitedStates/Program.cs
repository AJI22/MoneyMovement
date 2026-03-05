using Microsoft.EntityFrameworkCore;
using MoneyMovement.Observability;
using MoneyMovement.Outbox;
using Rails.UnitedStates;
using Rails.UnitedStates.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("Rails.UnitedStates");
builder.Services.AddHealthChecks().AddDefaultHealthCheck();

var conn = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=rails_us;Username=postgres;Password=postgres";
builder.Services.AddDbContext<UsRailDbContext>(o => o.UseNpgsql(conn));
builder.Services.AddScoped<IOutboxStore, OutboxStore<UsRailDbContext>>();
builder.Services.AddOutboxPublisher(useInMemoryBus: true);

builder.Services.AddHttpClient<ILedgerClient, LedgerClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["LedgerService:BaseUrl"] ?? "http://ledger-service:8080/");
});
builder.Services.AddScoped<UsRailService>();

var simDown = builder.Configuration.GetValue<bool>("SimulateUsProviderDown", false);
if (simDown)
{
    builder.Services.AddSingleton<IUnitedStatesPayoutProvider>(new FlakyDownProvider(0.9));
    builder.Services.AddSingleton<IUnitedStatesPayoutProvider>(new AlwaysSuccessProvider());
}
else
{
    builder.Services.AddSingleton<IUnitedStatesPayoutProvider>(new AlwaysSuccessProvider());
    builder.Services.AddSingleton<IUnitedStatesPayoutProvider>(new FlakyDownProvider(0.3));
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
