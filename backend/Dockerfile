# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY Splitr.sln .
COPY src/Splitr.API/Splitr.API.csproj src/Splitr.API/
COPY src/Splitr.Application/Splitr.Application.csproj src/Splitr.Application/
COPY src/Splitr.Domain/Splitr.Domain.csproj src/Splitr.Domain/
COPY src/Splitr.Infrastructure/Splitr.Infrastructure.csproj src/Splitr.Infrastructure/
COPY tests/Splitr.Tests/Splitr.Tests.csproj tests/Splitr.Tests/

RUN dotnet restore

# Copy everything and publish
COPY . .
RUN dotnet publish src/Splitr.API -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Splitr.API.dll"]
