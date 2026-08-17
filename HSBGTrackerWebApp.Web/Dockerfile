FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["HSBGTrackerWebApp.Web/HSBGTrackerWebApp.Web.csproj", "HSBGTrackerWebApp.Web/"]
# Add project references if the Web project depends on shared libraries

RUN dotnet restore "HSBGTrackerWebApp.Web/HSBGTrackerWebApp.Web.csproj"
COPY . .
WORKDIR /src/HSBGTrackerWebApp.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Docker
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HSBGTrackerWebApp.Web.dll"]