# vertr-market — Agent Instructions

## Stack
- .NET 10.0 | C# | `Market.slnx` (XML solution)
- Shared props: `Directory.Build.props` (ImplicitUsings, Nullable, TreatWarningsAsErrors, Workleap.DotNet.CodingStandards 2.0.5)
- All projects target `net10.0` — do not downgrade TFMs.

## Project layout (`src/`)

| Project | Role |
|---|---|
| `Market.Core` | Domain model, trading algorithms
| `Market.Infrastructure` | Pub/Sub stuff |
| `Market.Gateways.Tinvest` | T-Invest bridge: streams market depth/trades via `Tinkoff.InvestApi`, writes to object store. |
| `Market.Host` | ASP.NET Web API host + BackgroundServices. Swagger UI, Prometheus scraping endpoint. |

## Key commands

```bash
# Build all
dotnet build Market.slnx

# Run host locally
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
- **Tinvest gateway** is a standalone worker app — not a host dependency.
- OpenTelemetry metrics (Prometheus) enabled; tracing is commented out in `Program.cs`.

## EditorConfig overrides (non-default)

`.editorconfig` disables several diagnostics to `none`: Do not re-enable these without team agreement.

