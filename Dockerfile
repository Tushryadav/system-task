# ─── Stage 1: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy csproj first (layer caching)
COPY SRAAS.Api.csproj ./
RUN dotnet restore

# Copy rest of source
COPY . ./

# Publish app
RUN dotnet publish SRAAS.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ─── Stage 2: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

# Create non-root user (works on Debian-based images)
RUN useradd -m -d /home/appuser -s /bin/bash appuser

# Copy published output
COPY --from=build /app/publish .

# Fix permissions
RUN chown -R appuser:appuser /app

USER appuser

# Render-safe port binding
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SRAAS.Api.dll"]
