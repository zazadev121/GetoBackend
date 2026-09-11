# ── Build stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apiprojnew.csproj", "."]
RUN dotnet restore "./apiprojnew.csproj"
COPY . .
RUN dotnet publish "./apiprojnew.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ── Runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# ── Render / containerised environment fixes ─────────────────────────────────

# Prevents inotify watcher crashes (Render has low inotify limits)
ENV DOTNET_USE_POLLING_FILE_WATCHER=true

# Bind to the port Render provides (defaults to 8080 if not set)
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

# ── GC tuning for low-memory containers (Render free tier = 512 MB) ──────────
# Server GC is optimised for throughput but uses too much RAM on free containers.
# Workstation GC is gentler and prevents OOM-kill (exit 139 / SIGSEGV).
ENV DOTNET_GCConservatoryMode=1
ENV DOTNET_GCHeapHardLimit=419430400
ENV DOTNET_EnableDiagnostics=0
ENV DOTNET_TieredCompilation=1

# Cap the thread pool to avoid spinning up too many threads on startup
ENV DOTNET_ThreadPool_UnfairSemaphoreSpinLimit=6

ENTRYPOINT ["dotnet", "apiprojnew.dll"]
