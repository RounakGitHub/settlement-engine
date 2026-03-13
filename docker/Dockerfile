FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/SettlementEngine.Api/SettlementEngine.Api.csproj", "src/SettlementEngine.Api/"]
COPY ["src/SettlementEngine.Application/SettlementEngine.Application.csproj", "src/SettlementEngine.Application/"]
COPY ["src/SettlementEngine.Domain/SettlementEngine.Domain.csproj", "src/SettlementEngine.Domain/"]
COPY ["src/SettlementEngine.Infrastructure/SettlementEngine.Infrastructure.csproj", "src/SettlementEngine.Infrastructure/"]
RUN dotnet restore "src/SettlementEngine.Api/SettlementEngine.Api.csproj"
COPY . .
WORKDIR "/src/src/SettlementEngine.Api"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SettlementEngine.Api.dll"]
