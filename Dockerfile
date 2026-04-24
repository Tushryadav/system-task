# ─── Stage 1: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj first (better layer caching)
COPY SRAAS.Api.csproj ./
RUN dotnet restore

# Copy rest of the source
COPY . ./

# Publish app
RUN dotnet publish SRAAS.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ─── Stage 2: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user (correct way)
RUN useradd -m -d /home/appuser -s /bin/bash appuser

# Copy published output
COPY --from=build /app/publish .

# Set ownership (important for non-root execution)
RUN chown -R appuser:appuser /app

USER appuser

# Set port
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SRAAS.Api.dll"]
