# Market.BacktestEngine

Консольный бэктестер стратегий `Market.Core` на записанных стаканах.

Приложение читает файл `books_*.bin.gz`, прогоняет через выбранную стратегию
каждый стакан, исполняет сигналы через `BacktestOrderExecution` и сохраняет
итоговый `Portfolio` в JSON. В конце прогона открытые позиции закрываются по
последнему пригодному стакану с учётом проскальзывания и комиссии.

## Сборка

```bash
dotnet build src/Market.BacktestEngine/Market.BacktestEngine.csproj
```

Запуск без установки:

```bash
dotnet run --project src/Market.BacktestEngine -- --help
```

## Параметры командной строки

| Параметр | Обязательный | По умолчанию | Описание |
|---|---|---|---|
| `--data <path>` | да | — | Файл исторических стаканов `books_*.bin.gz` |
| `--metadata <path>` | да | — | JSON-файл с `AlgoMetadata` |
| `--output <path>` | нет | `portfolio.json` | Куда сохранить `Portfolio` |
| `--from <date-time>` | нет | начало файла | Начало интервала, включительно |
| `--to <date-time>` | нет | конец файла | Конец интервала, включительно |
| `--slippage-percent <value>` | нет | `0` | Проскальзывание в процентах (`0.05` = 0.05%) |
| `--commission-percent <value>` | нет | `0` | Комиссия в процентах от объёма сделки |
| `-h`, `--help` | нет | — | Показать справку |

Даты принимаются в ISO 8601. Если часовой пояс не указан, время считается
UTC. Например: `2026-10-05T10:00:00Z`, `2026-10-05T13:00:00+03:00`,
`2026-10-05`.

Если `--from` больше `--to`, приложение завершится с ошибкой конфигурации.

## Метаданные стратегии

Формат совпадает с телом `POST /algos` из `Market.Host`:

```json
{
  "strategyId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
  "assetX": 1,
  "assetY": 5,
  "algoType": "KalmanFilterBasicStrategy",
  "algoParams": {
    "EntryThreshold": 2.0,
    "ExitThreshold": 0.5,
    "MaxLots": 10,
    "WarmupTicks": 50
  }
}
```

Готовый пример: [`examples/kalman-filter-basic.json`](examples/kalman-filter-basic.json).

Обязательные поля:

- `strategyId` — непустой GUID;
- `assetX`, `assetY` — идентификаторы активов, должны различаться;
- `algoType` — имя класса из `AlgoFactory`
  (`KalmanFilterBasicStrategy`, `KalmanMidPriceAlgo`, `DummyRandomAlgo` и т.д.).

`algoParams` необязателен: отсутствующие параметры берутся из значений по
умолчанию конструктора стратегии. Неизвестные ключи игнорируются. Если
параметры нарушают ограничения стратегии, приложение сообщает об ошибке
конфигурации и завершается с кодом `2`.

## Результат

В `--output` записывается JSON с содержимым `Portfolio`:

```json
{
  "strategyId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
  "positions": [
    {
      "assetId": 1,
      "state": "None",
      "quantity": 0,
      "price": 0,
      "cash": -33.21,
      "commission": 0.12
    }
  ],
  "trades": [
    {
      "strategyId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
      "assetId": 1,
      "qty": 1,
      "price": 282,
      "commission": 0.14,
      "timestamp": "2026-10-05T16:22:22.291247Z"
    }
  ],
  "commission": 0.12
}
```

Поля `positions`, `trades` и `commission` позволяют посчитать финансовый
результат без повторного запуска бэктеста.

## Примеры запуска

### Базовый прогон

```bash
dotnet run --project src/Market.BacktestEngine -- \
  --data tests/Market.Core.Tests/Data/books_2026-10-05.bin.gz \
  --metadata src/Market.BacktestEngine/examples/kalman-filter-basic.json
```

Результат будет сохранён в `portfolio.json` в текущем каталоге.

### С интервалом дат, издержками и своим путём

```bash
dotnet run --project src/Market.BacktestEngine -- \
  --data tests/Market.Core.Tests/Data/books_2026-10-06.bin.gz \
  --metadata src/Market.BacktestEngine/examples/kalman-filter-basic.json \
  --from 2026-10-06T10:00:00Z \
  --to 2026-10-06T18:00:00Z \
  --slippage-percent 0.02 \
  --commission-percent 0.05 \
  --output results/kalman-2026-10-06.json
```

### Справка

```bash
dotnet run --project src/Market.BacktestEngine -- --help
```

## Как считается результат

1. Стаканы читаются из файла и фильтруются по `--from`/`--to`.
2. `strategy.ProcessBook(book)` возвращает сигнал.
3. `BacktestOrderExecution` исполняет обе ноги по уровням стакана:
   покупка — по `asks`, продажа — по `bids`, с частичным исполнением и
   дроблением на трейды.
4. Сделки применяются к `Portfolio`, после чего фактическая позиция
   передаётся в стратегию через `ReconcilePosition`.
5. В конце остаток позиции закрывается обратным рыночным ордером по
   последнему пригодному стакану. Для закрытия снимается ограничение доли
   уровня, чтобы позиция закрылась полностью, но проскальзывание и комиссия
   сохраняются.

## Тесты

```bash
dotnet test tests/Market.BacktestEngine.Tests/Market.BacktestEngine.Tests.csproj
```

Покрываются разбор аргументов, загрузка и валидация метаданных, параметры
конструкторов стратегий, фильтрация по датам, закрытие позиций и запись
`Portfolio` в JSON.
