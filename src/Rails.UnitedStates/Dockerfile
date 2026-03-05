FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/BuildingBlocks/Contracts/Contracts.csproj", "BuildingBlocks/Contracts/"]
COPY ["src/BuildingBlocks/Observability/Observability.csproj", "BuildingBlocks/Observability/"]
COPY ["src/BuildingBlocks/Outbox/Outbox.csproj", "BuildingBlocks/Outbox/"]
COPY ["src/BuildingBlocks/Http/Http.csproj", "BuildingBlocks/Http/"]
COPY ["src/Rails.UnitedStates/Rails.UnitedStates.csproj", "Rails.UnitedStates/"]
RUN dotnet restore "Rails.UnitedStates/Rails.UnitedStates.csproj"
COPY src/ .
RUN dotnet build "Rails.UnitedStates/Rails.UnitedStates.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Rails.UnitedStates/Rails.UnitedStates.csproj" -c Release -o /app/publish

FROM base AS final
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Rails.UnitedStates.dll"]
