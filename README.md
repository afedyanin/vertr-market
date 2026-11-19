# Система алгоритмического трейднига

Реализует типовые стратегии парного трейдинга (статистический арбитраж) на основе фильтра Калмана.

## Инфраструктура

Система использует внешнюю инфраструктуру, развернутую в Docker:

- redis
- redisinsight
- grafana
- prometheus

см. файл [compose.yaml](/infra/compose.yaml)

## Структура кода

### Solution Market.slnx

Структура папок :

- `src` - основной код
- `tests` - тесты в формате NUnit и тестовые консольные приложения
- `infra` - содержит compose.yaml файл с описанием используемой инфраструктуры, разворачиваемой в Docker-е
- `docs` - техническая документация

### Основной код

- `src\Market.Core\Market.Core.csproj`: доменная модель, основные стратегии и алгоритмы для трейдинга

- `src\Market.Gateways.Tinvest\Market.Gateways.Tinvest.csproj`: хост для взаимодействия с [T-инвест API](https://developer.tbank.ru/invest/api) 
-- Слушает стрим стаканов (order books) и отправляет маркет дату в Redis через механизм pub/sub 
-- Слушает торговые сигналы стратегий из Redis и реализует исполнение ордеров через T-инвест API
-- Слушает стрим сделок (трейдов) по исполненным ордерам и отправляет их в Redis
-- Реализует Web API для возможности взаимодействия с T-инвест API через Rest (swagger)

- `src\Market.Host\Market.Host.csproj`: Web API хост для создания и исполнения стратегий. 
-- Контейнер для хоста стратегий
-- Предоставляет Web API по управлению стратегиями (создание, получение статуса, удаление)
-- Предоставляет Web API для запроса отчетности по стратегии (портфель, позиции, PnL и т.п.) 
-- Слушает поток маркет данных (order books) из Redis (pub/sub) и транслирует их в активные стратегии
-- Отправляет тороговые сигналы стратегий в Redis
-- Слушает поток сделок (трейдов) по каждой стратегии из Redis и обновляет позиции и портфель стратегии

- `src\Market.ApiClient\Market.ApiClient.csproj`
-- Refit Rest API клиент для хоста стратегий (Market.Host)

- `src\Market.FileWriter\Market.FileWriter.csproj`
-- Служебная утилита, позволяющая записывать в бинарный файл поток данных (order books) из Redis, для бэктестов.

- `src\Market.Infrastructure\Market.Infrastructure.csproj`
-- вспомогательные классы и сущности для взаимодействия с инфраструктурой.

## Стек

- .NET 10.0 | C# | `Market.slnx` (XML solution)
- Shared props: `Directory.Build.props` (ImplicitUsings, Nullable, TreatWarningsAsErrors, Workleap.DotNet.CodingStandards 2.0.5)
- All projects target `net10.0` — do not downgrade TFMs.

## Key commands

```bash
# Build all
dotnet build Market.slnx

# Docker compose (host + tinvest)
# Requires external network: vertr-infrastructure_infra
docker compose -f compose.yaml up --build

```

## EditorConfig overrides (non-default)

`.editorconfig` disables several diagnostics to `none`: Do not re-enable these without team agreement.














