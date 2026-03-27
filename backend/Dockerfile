# ── Stage 1: build & publish ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies first (layer-cached until .csproj changes)
COPY MOMBotPro.API/MOMBotPro.API.csproj MOMBotPro.API/
RUN dotnet restore MOMBotPro.API/MOMBotPro.API.csproj

# Copy the rest of the source and publish
COPY . .
RUN dotnet publish MOMBotPro.API/MOMBotPro.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: runtime image ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "MOMBotPro.API.dll"]
