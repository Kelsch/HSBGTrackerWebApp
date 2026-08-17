# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for better layer caching
COPY ["HSBGTrackerWebApp.ApiService/HSBGTrackerWebApp.ApiService.csproj", "HSBGTrackerWebApp.ApiService/"]
COPY ["HSBGTrackerWebApp.ServiceDefaults/HSBGTrackerWebApp.ServiceDefaults.csproj", "HSBGTrackerWebApp.ServiceDefaults/"]
COPY ["HSBGTracker.Core/HSBGTracker.Core.csproj", "HSBGTracker.Core/"]
# etc.

RUN dotnet restore "HSBGTrackerWebApp.ApiService/HSBGTrackerWebApp.ApiService.csproj"

COPY . .
WORKDIR /src/HSBGTrackerWebApp.ApiService
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create directory for the SQLite file
RUN mkdir -p /data

COPY --from=build /app/publish .

# Use the Docker-specific settings
ENV ASPNETCORE_ENVIRONMENT=Docker
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "HSBGTrackerWebApp.ApiService.dll"]