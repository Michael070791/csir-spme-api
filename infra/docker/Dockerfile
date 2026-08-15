FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props Csir.Spme.sln ./
COPY src/Csir.Spme.Domain/Csir.Spme.Domain.csproj src/Csir.Spme.Domain/
COPY src/Csir.Spme.Application/Csir.Spme.Application.csproj src/Csir.Spme.Application/
COPY src/Csir.Spme.Infrastructure/Csir.Spme.Infrastructure.csproj src/Csir.Spme.Infrastructure/
COPY src/Csir.Spme.ServiceDefaults/Csir.Spme.ServiceDefaults.csproj src/Csir.Spme.ServiceDefaults/
COPY src/Csir.Spme.Api/Csir.Spme.Api.csproj src/Csir.Spme.Api/
COPY tools/Csir.Spme.Tools.DatabaseMigrator/Csir.Spme.Tools.DatabaseMigrator.csproj tools/Csir.Spme.Tools.DatabaseMigrator/
RUN dotnet restore src/Csir.Spme.Api/Csir.Spme.Api.csproj
RUN dotnet restore tools/Csir.Spme.Tools.DatabaseMigrator/Csir.Spme.Tools.DatabaseMigrator.csproj

COPY . .
RUN dotnet publish src/Csir.Spme.Api/Csir.Spme.Api.csproj -c Release -o /app/publish --no-restore

FROM build AS migrator-build
RUN dotnet publish tools/Csir.Spme.Tools.DatabaseMigrator/Csir.Spme.Tools.DatabaseMigrator.csproj -c Release -o /app/migrator --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime-base
WORKDIR /app

FROM runtime-base AS migrator
COPY --from=migrator-build /app/migrator .
ENTRYPOINT ["dotnet", "Csir.Spme.Tools.DatabaseMigrator.dll"]

FROM runtime-base AS api
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Csir.Spme.Api.dll"]
