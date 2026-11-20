# vertr-market — парный трейдинг на фильтре Калмана

Система алгоритмической торговли типовыми стратегиями парного трейдинга
(статистический арбитраж) на основе фильтра Калмана.

> **Статус:** исследовательский проект и sandbox-торговля. Система не является
> инвестиционной рекомендацией и не рассчитана на боевую торговлю без
> дополнительной проверки, ограничения рисков и аудита.

## Содержание

- [Архитектура](#архитектура)
- [Быстрый старт](#быстрый-старт)
- [Проекты solution](#проекты-solution)
- [HTTP API](#http-api)
- [Конфигурация](#конфигурация)
- [Потоки данных в Redis](#потоки-данных-в-redis)
- [Формат файлов order books](#формат-файлов-order-books)
- [Наблюдаемость](#наблюдаемость)
- [Тесты и бенчмарки](#тесты-и-бенчмарки)
- [Ограничения и известные проблемы](#ограничения-и-известные-проблемы)
- [Разработка](#разработка)
- [Документация](#документация)
- [Лицензия](#лицензия)

## Архитектура

```mermaid
flowchart LR
    TInvest[T-Invest API] -->|order books / trades / orders, gRPC| Gateway[Market.Gateways.Tinvest]
    Gateway -->|books:{assetId}| Redis[(Redis pub/sub)]
    Gateway -->|trades:{assetId}| Redis
    Redis -->|books:*| Host[Market.Host]
    Host -->|signals:{assetX}:{assetY}| Redis
    Redis -->|signals:*| Gateway
    Gateway -->|orders| TInvest
    Redis -->|trades:*| Host
    Host --> Portfolio[Портфели и позиции в памяти]
```

Основные процессы:

1. **Market.Gateways.Tinvest** подписывается на стрим стаканов T-Invest,
   публикует стаканы в Redis и исполняет торговые сигналы через T-Invest API.
2. **Market.Host** получает стаканы из Redis, прогоняет их через активные
   стратегии, публикует сигналы и обновляет позиции/портфели по сделкам.
   После каждой фактической сделки портфель передаёт стратегии сверенную
   позицию (`ReconcilePosition`), поэтому выход считается от исполненного
   объёма, а не от намерения стратегии.
3. **Market.FileWriter** записывает поток стаканов в сжатые файлы для
   последующего анализа и воспроизведения.

## Быстрый старт

### Требования

- .NET SDK **10.0.4xx** (`dotnet --version`);
- Docker Desktop / Docker Engine с Compose v2;
- токен и sandbox-аккаунт T-Invest API.

### 1. Запуск инфраструктуры

```bash
docker compose -f infra/compose.yaml up -d
```

Будут запущены:

| Сервис | Адрес |
|---|---|
| Redis | `localhost:6379` |
| RedisInsight | http://localhost:5540 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |

Compose-проект называется `infra`, поэтому создаётся внешняя сеть
`infra_infra`. Именно её ожидает корневой `compose.yaml`.

### 2. Настройка секретов

Скопируйте пример и заполните значения:

```bash
cp .env.example .env
```

```powershell
# Windows PowerShell
Copy-Item .env.example .env
```

```dotenv
TINVEST_TOKEN=<токен sandbox-доступа>
TINVEST_ACCOUNT_ID=<идентификатор sandbox-аккаунта>
```

`.env` не должен попадать в git. Не коммитьте `appsettings.*.json` с реальными
токенами: в репозитории допустимы только плейсхолдеры и example-файлы.

### 3. Запуск приложения

```bash
docker compose -f compose.yaml up --build
```

| Сервис | Docker | Локально |
|---|---|---|
| Market.Host | http://localhost:7001 | http://localhost:5001 |
| Market.Gateways.Tinvest | http://localhost:7002 | http://localhost:5002 |
| Swagger UI | `/swagger` | `/swagger` |
| Prometheus-метрики | `/metrics` | `/metrics` |

Проверка:

```bash
curl http://localhost:7001/algos
curl http://localhost:7001/portfolios
```

### Локальный запуск без Docker

Инфраструктура (Redis) должна быть запущена. Токен передайте переменной
окружения, чтобы не хранить его в файле. Если рядом есть локальный
`appsettings.Development.json`, он имеет приоритет над `appsettings.json`:

```bash
# bash
export TinvestSettings__InvestApiSettings__AccessToken=<token>
export TinvestSettings__AccountId=<account-id>

dotnet run --project src/Market.Gateways.Tinvest
dotnet run --project src/Market.Host
```

```powershell
# PowerShell
$env:TinvestSettings__InvestApiSettings__AccessToken = "<token>"
$env:TinvestSettings__AccountId = "<account-id>"

dotnet run --project src/Market.Gateways.Tinvest
dotnet run --project src/Market.Host
```

Порты локального запуска заданы в `Properties/launchSettings.json`
(`5001` и `5002`). Для запуска с профилем `Docker` вне compose можно
использовать `appsettings.Docker.example.json` как основу для локального
`appsettings.Docker.json`.

## Проекты solution

| Проект | Назначение |
|---|---|
| `src/Market.Core` | Доменная модель, стратегии, фильтры Калмана, калькуляторы лотов, чтение файлов стаканов |
| `src/Market.Gateways.Tinvest` | Web API и фоновые сервисы T-Invest: стримы стаканов/сделок, исполнение ордеров, REST-прокси |
| `src/Market.Host` | Web API управления стратегиями и портфелями, runtime стратегий |
| `src/Market.Infrastructure` | Redis publishers/subscribers, реализации репозиториев, общие сервисы |
| `src/Market.ApiClient` | DTO-контракты ответов Host API. HTTP-клиент (Refit) пока не реализован |
| `src/Market.FileWriter` | Утилита записи потока стаканов из Redis в `books/books_YYYY-MM-DD.bin.gz` |
| `src/Market.BacktestEngine` | Консольный бэктестер: прогон стратегии по записанным стаканам в `Portfolio` JSON |
| `tests/Market.Core.Tests` | NUnit-тесты ядра |
| `tests/Market.Infrastructure.Tests` | NUnit-тесты инфраструктуры |
| `tests/Market.BacktestEngine.Tests` | NUnit-тесты параметров и runner консольного бэктестера |
| `tests/Market.Benchmarks` | NBomber-бенчмарк |

## HTTP API

### Market.Host

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/algos` | Список стратегий |
| `POST` | `/algos` | Создание стратегии. Повторный `strategyId` возвращает `400` |
| `DELETE` | `/algos/{strategyId:guid}` | Удаление стратегии |
| `GET` | `/portfolios` | Все портфели |
| `GET` | `/portfolios/{strategyId:guid}` | Портфель стратегии |
| `GET` | `/swagger` | Swagger UI |
| `GET` | `/openapi/v1.json` | OpenAPI-документ |
| `GET` | `/metrics` | Prometheus-метрики |

Обновления стратегии (`PUT/PATCH`) и отдельного endpoint’а статуса сейчас нет.
Стратегии и портфели хранятся в памяти процесса и теряются при рестарте.

Пример создания стратегии:

```bash
curl -X POST http://localhost:7001/algos \
  -H "Content-Type: application/json" \
  -d '{
    "strategyId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
    "assetX": 1,
    "assetY": 5,
    "algoType": "KalmanMidPriceAlgo",
    "algoParams": {
      "LotSizeX": 1,
      "LotSizeY": 100,
      "InitialBeta": 1.0,
      "InitialErrorCovariance": 1.0
    }
  }'
```

### Market.Gateways.Tinvest

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/api/accounts` | Счета T-Invest |
| `GET` | `/api/accounts/sandbox` | Sandbox-счета |
| `POST` | `/api/accounts/sandbox?accountName=...` | Открыть sandbox-счёт |
| `DELETE` | `/api/accounts/sandbox/{accountId}` | Закрыть sandbox-счёт |
| `PUT` | `/api/accounts/sandbox/{accountId}` | Пополнение sandbox-счёта (`Money`) |
| `GET` | `/api/accounts/positions/{accountId}?isSandbox=true` | Позиции |
| `GET` | `/api/assets/find?query=...` | Поиск инструмента |
| `GET` | `/api/assets/by-ticker/{classCode}/{ticker}?rawData=false` | Инструмент по тикеру |
| `GET` | `/api/assets/share-by-id/{shareId:guid}?rawData=false` | Акция по UID |
| `GET` | `/api/assets/future-by-id/{futureId:guid}?rawData=false` | Фьючерс по UID |
| `POST` | `/api/orders` | Ручная отправка ордера |
| `GET` | `/api/subscriptions` | Текущие подписки на стаканы |
| `GET` | `/swagger` | Swagger UI |
| `GET` | `/metrics` | Prometheus-метрики |

В `POST /api/orders` знак `qtyLots` задаёт направление (`> 0` — покупка,
`< 0` — продажа), `price = 0` означает рыночный ордер.

## Конфигурация

Настройки читаются из `appsettings.json`, `appsettings.{Environment}.json`,
переменных окружения и аргументов командной строки. В Docker используется
`ASPNETCORE_ENVIRONMENT=Docker`.

| Ключ | Переменная окружения | По умолчанию | Примечание |
|---|---|---|---|
| `ConnectionStrings:Redis` | `ConnectionStrings__Redis` | `localhost:6379` | В Docker — `redis:6379` |
| `TinvestSettings:InvestApiSettings:AccessToken` | `TinvestSettings__InvestApiSettings__AccessToken` | `<API_TOKEN>` | Секрет, только через env/secret store |
| `TinvestSettings:InvestApiSettings:Sandbox` | `TinvestSettings__InvestApiSettings__Sandbox` | `true` | Sandbox-режим |
| `TinvestSettings:AccountId` | `TinvestSettings__AccountId` | sandbox-id | Счёт для ордеров |
| `TinvestSettings:Subscriptions` | `TinvestSettings__Subscriptions__0`, `__1`, … | список тикеров | Формат `CLASSCODE:TICKER` |
| `TinvestSettings:OrderBookDepth` | `TinvestSettings__OrderBookDepth` | `10` | Допустимо 1, 10, 20, 30, 40, 50 |
| `TinvestSettings:DataStreamEnabled` | `TinvestSettings__DataStreamEnabled` | `true` | Подписка на стаканы |
| `TinvestSettings:TradesStreamEnabled` | `TinvestSettings__TradesStreamEnabled` | `true` | Подписка на сделки |
| `TinvestSettings:StaticDataDirectory` | `TinvestSettings__StaticDataDirectory` | `JsonStaticData` | Каталог JSON-справочников |
| `TinvestSettings:OrderRequestTimeout` | `TinvestSettings__OrderRequestTimeout` | `10s` | Таймаут запроса ордера |
| `TinvestSettings:SignalMaxAge` | `TinvestSettings__SignalMaxAge` | `5s` | Устаревшие сигналы отбрасываются |
| `MarketApiSettings:BaseUrl` | `MarketApiSettings__BaseUrl` | — | Сейчас не используется |
| `TinvestSettings:OutputDirectory` | — | `tinvest` | Сейчас не используется |
| `TinvestSettings:TradeAggergationInterval` | — | `5s` | Сейчас не используется |

`JsonStaticData` содержит полный набор инструментов, `JsonStaticDataTest` —
минимальный набор `TQBR:AFKS` и `TQBR:AFLT` для Docker-профиля по умолчанию.
Пустые элементы в `Subscriptions` игнорируются, поэтому Docker-профиль
переопределяет массив из `appsettings.json` только двумя подписками.

## Потоки данных в Redis

Обмен между процессами идёт через Redis pub/sub. Сообщения сериализованы в JSON.

| Канал | Публикует | Читает | Payload |
|---|---|---|---|
| `books:{assetId}` | `Market.Gateways.Tinvest` | `Market.Host`, `Market.FileWriter` | `OrderBookDepth` |
| `trades:{assetId}` | `Market.Gateways.Tinvest` | `Market.Host` | `MyTrade` |
| `signals:{assetX}:{assetY}` | `Market.Host` | `Market.Gateways.Tinvest` | `PairTradingSignal` |
| `orders:requests:{assetId}` | `Market.Gateways.Tinvest` | внешние потребители | `PostOrderRequest` |
| `orders:responses:{assetId}` | `Market.Gateways.Tinvest` | внешние потребители | ответ T-Invest |
| `orders:errors:{assetId}` | `Market.Gateways.Tinvest` | внешние потребители | `OrderErrorEvent` (ошибка/таймаут) |
| `orders:trades:{assetId}` | `Market.Gateways.Tinvest` | внешние потребители | `OrderTrades` |

Публикация выполняется с `CommandFlags.FireAndForget`, поэтому доставка не
гарантируется. Это pub/sub, а не очередь: при отсутствии подписчика сообщение
теряется.

Если одна нога пары отклонена биржей, исполненная нога закрывается обратным
рыночным ордером. Если состояние второй ноги неизвестно (таймаут, сетевой
сбой), автоматическое хеджирование не выполняется — сервис пишет
`Critical` и останавливает хост, чтобы позиция не была закрыта дважды без
участия оператора.

## Формат файлов order books

`Market.FileWriter` пишет файлы `books/books_YYYY-MM-DD.bin.gz`:

- gzip-сжатый поток;
- батчи `List<OrderBookDepth>` по 1000 записей или раз в 5 секунд;
- сериализация MessagePack;
- чтение — `Market.Core.FileStore.OrderBookFileReader.ReadAsync`.

Файлы примера лежат в `tests/Market.Core.Tests/Data`. Полный прогон с
исполнением сигналов, сверкой позиции и закрытием открытых позиций в конце
есть в `KalmanFilterBasicStrategyTestsBack` и в консольном
`Market.BacktestEngine`. Исполнение делает эмулятор
`Market.Infrastructure.BacktestOrderExecution`: он принимает
`PairTradingSignal` и возвращает `MyTrade[]`, исполняя заявки по уровням
стакана. Проскальзывание и комиссия задаются в процентах, а
`BacktestExecutionOptions` управляет дроблением на трейды
(`MaxTradeLots`) и долей доступной ликвидности (`MaxLevelParticipation`).
Проскальзывание меняет цену исполнения, комиссия пишется в отдельное поле
`MyTrade.Commission` и уменьшает `Cash` портфеля.
Если ликвидности в стакане не хватает, заявка исполняется частично.

## Наблюдаемость

- Оба Web-хоста отдают Prometheus-метрики по `/metrics`.
- `infra/prometheus.yml` scrape-ит `vertr-market-tinvest-1:8080` и
  `vertr-market-host-1:8080`. Имена контейнеров стабильны благодаря
  `name: vertr-market` в корневом `compose.yaml`.
- Grafana доступна на `http://localhost:3000`; источник данных Prometheus
  (`http://prometheus:9090`) добавляется вручную.
- В `Market.Host` трейсинг настроен на OTLP gRPC `http://localhost:4317`, но
  collector/Jaeger в `infra/compose.yaml` закомментирован. В текущей
  конфигурации трейсы фактически никуда не отправляются.

## Тесты и бенчмарки

```bash
# Все тесты
dotnet test Market.slnx

# Только тесты ядра
dotnet test tests/Market.Core.Tests/Market.Core.Tests.csproj

# Бенчмарк NBomber (около 60 секунд)
dotnet run --project tests/Market.Benchmarks

# Бэктест стратегии по записанным стаканам
dotnet run --project src/Market.BacktestEngine -- \
  --data tests/Market.Core.Tests/Data/books_2026-10-05.bin.gz \
  --metadata src/Market.BacktestEngine/examples/kalman-filter-basic.json
```

`Market.BacktestEngine` работает офлайн: читает файл стаканов, метаданные
стратегии и сохраняет итоговый `Portfolio` в JSON. Подробности и параметры —
в `src/Market.BacktestEngine/README.md`.

## Ограничения и известные проблемы

- **Состояние в памяти.** Стратегии и портфели хранятся в
  `ConcurrentDictionary` и теряются при перезапуске Host.
- **Нет обновления стратегий.** `POST /algos` только создаёт запись; повторный
  `strategyId` возвращает `400`. Endpoint’а статуса нет.
- **Нет аутентификации и авторизации** у Web API. Не выставляйте порты в
  публичную сеть.
- **Pub/sub без гарантий доставки.** Пропущенные сообщения не восстанавливаются.
- **Unbounded-каналы.** Медленный потребитель может привести к росту памяти.
- **Один общий Redis.** Каналы не namespaced по окружениям; несколько
  экземпляров системы будут мешать друг другу.
- **Неполный трейсинг.** OTLP-endpoint задан жёстко, collector не запущен.
- **Sandbox по умолчанию.** Проверяйте `Sandbox` и `AccountId` перед запуском
  на реальном счёте.
- **Секреты.** Реальные токены не должны храниться в `appsettings*.json`;
  используйте `.env`/переменные окружения и не добавляйте `.env` в git.

## Разработка

- Целевой framework — `net10.0`, задан в `Directory.Build.props`.
- Включены `Nullable`, `ImplicitUsings` и `TreatWarningsAsErrors`.
- Стиль и отключённые анализаторы — в `.editorconfig`. Не включайте
  отключённые правила без согласования с командой.
- Перед PR:

  ```bash
  dotnet build Market.slnx
  dotnet test Market.slnx
  ```

- Новый алгоритм добавляется реализацией `IPairTradingAlgo` и регистрацией в
  `AlgoFactory.Create`.
- Параметры стратегии описываются ключами `AlgoMetadata.Keys` и передаются в
  `algoParams` при создании.

## Документация

- [`docs/qty2lots.md`](docs/qty2lots.md) — черновик по расчёту лотов из
  коэффициента beta. В коде используется `ApproximateLotCalculator`; примеры из
  документа могут не совпадать с текущим API.

## Лицензия

Файл `LICENSE` в репозитории отсутствует, условия использования не определены.
