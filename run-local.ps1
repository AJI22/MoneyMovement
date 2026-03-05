# Start infrastructure and run migrations + seed ledger
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "Starting Docker Compose (postgres, tigerbeetle)..."
docker compose up -d postgres tigerbeetle
Start-Sleep -Seconds 8

Write-Host "Creating databases..."
$dbs = @("ledger", "rails_ng", "rails_us", "fx", "orchestrator")
foreach ($db in $dbs) {
    docker compose exec -T postgres psql -U postgres -tc "SELECT 1 FROM pg_database WHERE datname = '$db'" 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        docker compose exec -T postgres psql -U postgres -c "CREATE DATABASE $db"
    }
}

$connBase = "Host=localhost;Port=5432;Username=postgres;Password=postgres"
Write-Host "Running EF migrations..."
dotnet ef database update --project src/Ledger.Service/Ledger.Service.csproj -- --connection "$connBase;Database=ledger"
dotnet ef database update --project src/Rails.Nigeria/Rails.Nigeria.csproj -- --connection "$connBase;Database=rails_ng"
dotnet ef database update --project src/Rails.UnitedStates/Rails.UnitedStates.csproj -- --connection "$connBase;Database=rails_us"
dotnet ef database update --project src/FX.Service/FX.Service.csproj -- --connection "$connBase;Database=fx"
dotnet ef database update --project src/Transfer.Orchestrator/Transfer.Orchestrator.csproj -- --connection "$connBase;Database=orchestrator"

Write-Host "Starting all services..."
docker compose up -d

Write-Host "Waiting for Ledger service (15s)..."
Start-Sleep -Seconds 15
try { Invoke-RestMethod -Uri "http://localhost:5080/ledger/bootstrap" -Method Post } catch { Write-Host "Bootstrap call failed (service may still be starting): $_" }

Write-Host "Local stack is up. Orchestrator: http://localhost:5084/swagger, Ledger: http://localhost:5080/swagger"
