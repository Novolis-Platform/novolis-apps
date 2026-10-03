# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .

# Mount an authenticated NuGet configuration at build time for GitHub Packages.
# Example: docker build --secret id=nuget_config,src=/absolute/path/NuGet.Config ...
RUN --mount=type=secret,id=nuget_config,target=/root/.nuget/NuGet/NuGet.Config \
    dotnet publish /src/src/NovolisHours/Novolis.Hours.Server/Novolis.Hours.Server.csproj \
      --configuration Release \
      --output /out \
      --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV Hours__DataPath=/data
ENV Hours__EnableDemoAdminCredentials=false
EXPOSE 8080
VOLUME ["/data"]
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Novolis.Hours.Server.dll"]
