using Reconciliation.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient("Ledger", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["LedgerService:BaseUrl"] ?? "http://localhost:5080/");
});
builder.Services.AddHostedService<Worker>();
var host = builder.Build();
await host.RunAsync();
