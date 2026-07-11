FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["NuGet.Config", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["AgroControl.slnx", "./"]
COPY ["apps/api/src/AgroControl.Api/AgroControl.Api.csproj", "apps/api/src/AgroControl.Api/"]
COPY ["apps/api/src/AgroControl.Application/AgroControl.Application.csproj", "apps/api/src/AgroControl.Application/"]
COPY ["apps/api/src/AgroControl.Infrastructure/AgroControl.Infrastructure.csproj", "apps/api/src/AgroControl.Infrastructure/"]
COPY ["apps/api/src/AgroControl.Contracts/AgroControl.Contracts.csproj", "apps/api/src/AgroControl.Contracts/"]
COPY ["apps/api/src/AgroControl.Domain/AgroControl.Domain.csproj", "apps/api/src/AgroControl.Domain/"]

RUN dotnet restore "apps/api/src/AgroControl.Api/AgroControl.Api.csproj" --configfile ./NuGet.Config

COPY . .
RUN dotnet publish "apps/api/src/AgroControl.Api/AgroControl.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AgroControl.Api.dll"]
