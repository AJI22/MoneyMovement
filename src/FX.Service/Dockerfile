FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/BuildingBlocks/Contracts/Contracts.csproj", "BuildingBlocks/Contracts/"]
COPY ["src/BuildingBlocks/Observability/Observability.csproj", "BuildingBlocks/Observability/"]
COPY ["src/BuildingBlocks/Outbox/Outbox.csproj", "BuildingBlocks/Outbox/"]
COPY ["src/BuildingBlocks/Http/Http.csproj", "BuildingBlocks/Http/"]
COPY ["src/FX.Service/FX.Service.csproj", "FX.Service/"]
RUN dotnet restore "FX.Service/FX.Service.csproj"
COPY src/ .
RUN dotnet build "FX.Service/FX.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FX.Service/FX.Service.csproj" -c Release -o /app/publish

FROM base AS final
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FX.Service.dll"]
