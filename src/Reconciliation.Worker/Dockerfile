FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/BuildingBlocks/Contracts/Contracts.csproj", "BuildingBlocks/Contracts/"]
COPY ["src/Reconciliation.Worker/Reconciliation.Worker.csproj", "Reconciliation.Worker/"]
RUN dotnet restore "Reconciliation.Worker/Reconciliation.Worker.csproj"
COPY src/ .
RUN dotnet build "Reconciliation.Worker/Reconciliation.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Reconciliation.Worker/Reconciliation.Worker.csproj" -c Release -o /app/publish

FROM base AS final
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Reconciliation.Worker.dll"]
