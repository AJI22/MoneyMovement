$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "Running unit tests with coverage..."
dotnet test tests/UnitTests/UnitTests.csproj --collect:"XPlat Code Coverage" --results-directory ./tests/coverage-output -l "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Running integration tests..."
dotnet test tests/IntegrationTests/IntegrationTests.csproj -l "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Tests completed."
