# vertr-market — Agent Instructions

## Stack
- .NET 10.0 | C# | `Market.slnx` (XML solution)
- Shared props: `Directory.Build.props` (ImplicitUsings, Nullable, TreatWarningsAsErrors, Workleap.DotNet.CodingStandards 2.0.5)
- All projects target `net10.0` — do not downgrade TFMs.

## Project layout (`src/`)

| Project | Role |
|---|---|
| `Market.ApiClient` | Shared library: REST (Refit) + TCP client abstractions. Has `InternalsVisibleTo Market.Benchmarks`. |
| `Market.Core` | Domain model, `IObjectStore<T>` abstraction, converters, TCP command parser/writer, DI registration (`ServiceRegistrar`). |
| `Market.Host` | ASP.NET Web API host + `TcpServer` BackgroundService. Swagger UI, Prometheus scraping endpoint. |
| `Market.Gateways.Tinvest` | T-Invest bridge: streams market depth/trades via `Tinkoff.InvestApi`, writes to object store. |
| `Market.ConsoleApp` | Demo CLI consumer using `Market.ApiClient`. |

## Key commands

```bash
# Build all
dotnet build Market.slnx

# Run host locally (HTTP :8080, TCP :8085)
dotnet run --project src/Market.Host

# Run Tinvest gateway
dotnet run --project src/Market.Gateways.Tinvest

# Run benchmark CLI
dotnet run --project tests/Market.Benchmarks

# Docker compose (host + tinvest)
# Requires external network: vertr-infrastructure_infra
docker compose -f compose.yaml up --build
```

## Architecture notes

- **Host** exposes REST (`/api/*`) and TCP (port 8085). TCP commands: `GetBooks`, `PostBooks`, `ClearBooks`, `DeleteBooksByAsset`.
- **ObjectStore** is in-memory only (`MarketDepthObjectStore`, `TradeTickObjectStore`), registered via `ServiceRegistrar.AddObjectStores()`.
- **Tinvest gateway** is a standalone worker app — not a host dependency.
- OpenTelemetry metrics (Prometheus) enabled; tracing is commented out in `Program.cs`.

## EditorConfig overrides (non-default)

`.editorconfig` disables several diagnostics to `none`: `IDE0009`, `IDE0040`, `CA1027`, `CA2254`, `CA1851`, `CA1305`, `CA1873`, `MA017`, `CA1051`, `CA1815`, `CA1028`, `IDE0007`, `CA1816`, `CA1063`. Do not re-enable these without team agreement.

## Constraints

- No unit test project — only `Market.Benchmarks` (NBomber + BenchmarkDotNet).
- No CI/CD, no pre-commit hooks, no `global.json`.
- Docker networks require external `vertr-infrastructure_infra` — cannot run compose in isolation.
- `Market.ApiClient` exposes internals to benchmarks via `InternalsVisibleTo`.
