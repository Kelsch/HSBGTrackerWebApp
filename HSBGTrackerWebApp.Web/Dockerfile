FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["HSBGTrackerWebApp.Web/HSBGTrackerWebApp.Web.csproj", "HSBGTrackerWebApp.Web/"]
COPY ["HSBGTrackerWebApp.ServiceDefaults/HSBGTrackerWebApp.ServiceDefaults.csproj", "HSBGTrackerWebApp.ServiceDefaults/"]
COPY ["HSBGTracker.Core/HSBGTracker.Core.csproj", "HSBGTracker.Core/"]
# Add project references if the Web project depends on shared libraries

RUN dotnet restore "HSBGTrackerWebApp.Web/HSBGTrackerWebApp.Web.csproj"
COPY . .
WORKDIR /src/HSBGTrackerWebApp.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# Debug: fail the build if Blazor framework files are missing
RUN echo "=== publish wwwroot ===" && \
    ls -la /app/publish/wwwroot && \
    echo "=== _framework ===" && \
    ls -la /app/publish/wwwroot/_framework && \
    test -f /app/publish/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Docker
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HSBGTrackerWebApp.Web.dll"]