# Pgsql Vertr Db Migrations

## Install EF tools

```shell
dotnet tool install --global dotnet-ef
```

## Create migrations

```shell
dotnet ef migrations add AccessControlTables --context AccessControlDbContext
```

## Run migrations

```shell
dotnet ef database update --context AccessControlDbContext
```

