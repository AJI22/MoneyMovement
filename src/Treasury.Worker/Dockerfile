FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/BuildingBlocks/Contracts/Contracts.csproj", "BuildingBlocks/Contracts/"]
COPY ["src/Treasury.Worker/Treasury.Worker.csproj", "Treasury.Worker/"]
RUN dotnet restore "Treasury.Worker/Treasury.Worker.csproj"
COPY src/ .
RUN dotnet build "Treasury.Worker/Treasury.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Treasury.Worker/Treasury.Worker.csproj" -c Release -o /app/publish

FROM base AS final
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Treasury.Worker.dll"]
