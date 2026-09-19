# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/TacticalAI.Headless/TacticalAI.Headless.csproj --locked-mode --configfile NuGet.Config -p:TargetFramework=net10.0
RUN dotnet publish src/TacticalAI.Headless/TacticalAI.Headless.csproj -c Release --no-restore --output /app -p:TargetFramework=net10.0

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "TacticalAI.Headless.dll"]
