FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/BuildingBlocks/Contracts/Contracts.csproj", "BuildingBlocks/Contracts/"]
COPY ["src/BuildingBlocks/Observability/Observability.csproj", "BuildingBlocks/Observability/"]
COPY ["src/BuildingBlocks/Outbox/Outbox.csproj", "BuildingBlocks/Outbox/"]
COPY ["src/BuildingBlocks/Http/Http.csproj", "BuildingBlocks/Http/"]
COPY ["src/Ledger.Service/Ledger.Service.csproj", "Ledger.Service/"]
RUN dotnet restore "Ledger.Service/Ledger.Service.csproj"
COPY src/ .
RUN dotnet build "Ledger.Service/Ledger.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ledger.Service/Ledger.Service.csproj" -c Release -o /app/publish

FROM base AS final
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ledger.Service.dll"]
