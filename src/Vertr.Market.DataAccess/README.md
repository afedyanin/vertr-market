# Pgsql Vertr Db Migrations

## Install EF tools

```shell
dotnet tool install --global dotnet-ef
```

## Create migrations

```shell
dotnet ef migrations add MarketDataTables01 --context MarketDataDbContext
```

## Run migrations

```shell
dotnet ef database update --context MarketDataDbContext
```

