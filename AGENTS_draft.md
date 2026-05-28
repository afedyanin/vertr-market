# vertr-market — agent instructions

## Tech stack
- **.NET 10.0 SDK** — target framework is `net10.0`.
- Database: PostgreSQL + Entity Framework Core (Code First)
- Additional: MediatR (CQRS), FluentValidation

## Architecture (3 layers)
Vertr.Market.Host      — entrypoint (ASP.NET Core Web API), DI, background services
Vertr.Market.Application — business logic, Disruptor-based processing, abstractions
vertr-common/           — shared clients & contracts
  Vertr.Common.Contracts       — OrderBook, MarketTrade models + abstractions
  Vertr.Common.Clients.Tinvest — T-invest API gateway client
  Vertr.Common.Clients.Moex    — MOEX API client

## Key files
- `src/Vertr.Market.Host/Program.cs` — app bootstrap, Redis/Tinvest wiring, hosted services registration
- `src/Vertr.Market.Application/ApplicationRegistrar.cs` — application DI registration
- `src/Vertr.Market.Host/BackgroundServices/` — `OrderBookSubscriber`, `MarketTradeSubscriber` (Redis pub/sub consumers)
- `src/Vertr.Market.Application/Services/PeriodicTradeProcessor.cs` — aggregates trades into bars
- `src/Vertr.Market.Application/Abstractions/AtomicCell.cs` — thread-safe atomic cell (Disruptor integration)
- `vertr-common/Vertr.Common.Contracts/` — shared domain models & interfaces

## Conventions
- `Directory.Build.props` enforces: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- Global analyzer: `Workleap.DotNet.CodingStandards` (v1.2.4).
- Test framework: **NUnit 4.x** with NUnit3TestAdapter. No inline `[SetUp]` overrides — use constructor or `[OneTimeSetUp]`.
- Tests reference `InternalsVisibleTo` from `Vertr.Market.Application` — test projects can access internal types.
- `Market.slnx` is the solution file.

## Правила написания кода C#
1. Используй File-Scoped Namespaces (например, `namespace MyProject.Api;`).
2. Для DTO и Command/Query всегда используй `public record`.
3. Все методы ввода-вывода должны быть асинхронными (`async Task`) и принимать `CancellationToken`.

## Run locally
- **Start**: `dotnet run Market.slnx`
- **Test**: `dotnet test`
- **Build**: `dotnet build Market.slnx`
