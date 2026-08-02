# ShowcaseDataService — multi-stage build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY BuildingBlocks/ ./BuildingBlocks/
COPY DataServices/ShowcaseDataService/ ./DataServices/ShowcaseDataService/
RUN dotnet restore DataServices/ShowcaseDataService/ShowcaseDataService.Api/ShowcaseDataService.Api.csproj
RUN dotnet publish DataServices/ShowcaseDataService/ShowcaseDataService.Api/ShowcaseDataService.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShowcaseDataService.Api.dll"]
