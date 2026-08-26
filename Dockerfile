# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apiprojnew.csproj", "."]
RUN dotnet restore "./apiprojnew.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./apiprojnew.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./apiprojnew.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Render sets PORT env var dynamically — bind Kestrel to it
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "apiprojnew.dll"]