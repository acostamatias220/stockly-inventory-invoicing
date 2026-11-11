# syntax=docker/dockerfile:1

# ---- build -------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached layer as long as project files do not change)
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Stockly.Domain/Stockly.Domain.csproj src/Stockly.Domain/
COPY src/Stockly.Application/Stockly.Application.csproj src/Stockly.Application/
COPY src/Stockly.Infrastructure/Stockly.Infrastructure.csproj src/Stockly.Infrastructure/
COPY src/Stockly.Api/Stockly.Api.csproj src/Stockly.Api/
RUN dotnet restore src/Stockly.Api/Stockly.Api.csproj

COPY src/ src/
RUN dotnet publish src/Stockly.Api/Stockly.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime -----------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# QuestPDF needs fontconfig to resolve the fonts it embeds in the invoices
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=build /app/publish .

# Non-root user shipped with the official .NET images
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Stockly.Api.dll"]
