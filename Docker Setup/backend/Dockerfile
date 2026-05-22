# =============================================================================
#  Backend — PRODUCTION Dockerfile  (multi-stage build)
#  Used for: Test environment, Demo environment, and Azure deployment
#  Place this file at:  backend/Dockerfile
# =============================================================================

# ── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project files and restore (layer cached until .csproj changes)
COPY *.sln ./
COPY src/GenService.Domain/GenService.Domain.csproj           src/GenService.Domain/
COPY src/GenService.Application/GenService.Application.csproj src/GenService.Application/
COPY src/GenService.Infrastructure/GenService.Infrastructure.csproj src/GenService.Infrastructure/
COPY src/GenService.API/GenService.API.csproj                 src/GenService.API/
COPY tests/GenService.UnitTests/GenService.UnitTests.csproj   tests/GenService.UnitTests/

RUN dotnet restore

# Copy all source and publish
COPY . .
RUN dotnet publish src/GenService.API/GenService.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

# Create non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build --chown=appuser:appgroup /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "GenService.API.dll"]
