# ─── Stage 1: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first (layer cache: only re-runs if deps change)
COPY SRAAS.Api.csproj ./
RUN dotnet restore

# Copy everything else and publish
COPY . ./
RUN dotnet publish SRAAS.Api.csproj -c Release -o /app/publish --no-restore

# ─── Stage 2: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Non-root user for security
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

# ASP.NET Core will read ASPNETCORE_URLS
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SRAAS.Api.dll"]
