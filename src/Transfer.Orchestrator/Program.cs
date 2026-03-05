using Microsoft.EntityFrameworkCore;
using MoneyMovement.Observability;
using MoneyMovement.Outbox;
using Transfer.Orchestrator;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("Transfer.Orchestrator");
builder.Services.AddHealthChecks().AddDefaultHealthCheck();

var conn = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=orchestrator;Username=postgres;Password=postgres";
builder.Services.AddDbContext<OrchestratorDbContext>(o => o.UseNpgsql(conn));
builder.Services.AddScoped<IOutboxStore, OutboxStore<OrchestratorDbContext>>();
builder.Services.AddOutboxPublisher(useInMemoryBus: true);

builder.Services.AddHttpClient("RailsNigeria", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["RailsNigeria:BaseUrl"] ?? "http://rails-nigeria:8080/");
});
builder.Services.AddHttpClient("RailsUnitedStates", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["RailsUnitedStates:BaseUrl"] ?? "http://rails-unitedstates:8080/");
});
builder.Services.AddHttpClient("FX", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["FX:BaseUrl"] ?? "http://fx-service:8080/");
});
builder.Services.AddHttpClient("Ledger", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ledger:BaseUrl"] ?? "http://ledger-service:8080/");
});
builder.Services.AddScoped<TransferFlowService>();

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
