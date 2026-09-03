FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release

COPY ["Directory.Build.props", "."]
COPY ["src/Market.Host/Market.Host.csproj", "Market.Host/"]

RUN dotnet restore "Market.Host/Market.Host.csproj"
COPY . .

RUN dotnet build "src/Market.Host/Market.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/Market.Host/Market.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

#ENV ASPNETCORE_ENVIRONMENT=Docker 
ENTRYPOINT ["dotnet", "Market.Host.dll"]
