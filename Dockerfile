# ─── Stage 1: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj first (better caching)
COPY SRAAS.Api.csproj ./
RUN dotnet restore

# Copy remaining source
COPY . ./

# Publish app (optimized)
RUN dotnet publish SRAAS.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ─── Stage 2: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user
RUN useradd -m -d /home/appuser -s /bin/bash appuser

# Copy published app
COPY --from=build /app/publish .

# Fix permissions
RUN chown -R appuser:appuser /app

USER appuser

# Configure port (Render expects 10000 sometimes, but 8080 is fine unless specified)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SRAAS.Api.dll"]
