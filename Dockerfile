# https://aka.ms/customizecontainer 

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY Directory.Build.props ./ 
COPY ["vertr-common/Vertr.Common.Contracts/Vertr.Common.Contracts.csproj", "Vertr.Common.Contracts/"]
COPY ["src/Vertr.Market.Application/Vertr.Market.Application.csproj", "Vertr.Market.Application/"]
COPY ["src/Vertr.Market.DataAccess/Vertr.Market.DataAccess.csproj", "Vertr.Market.DataAccess/"]
COPY ["src/Vertr.Market.Host/Vertr.Market.Host.csproj", "Vertr.Market.Host/"]

RUN dotnet restore "Vertr.Market.Host/Vertr.Market.Host.csproj"
COPY . .

RUN dotnet build "src/Vertr.Market.Host/Vertr.Market.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/Vertr.Market.Host/Vertr.Market.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

#ENV ASPNETCORE_ENVIRONMENT=Docker 
ENTRYPOINT ["dotnet", "Vertr.Market.Host.dll"]

