# vertr-market

## Система обработки и хранения рыночных данных

Сервис слушает потоки рыночных котировок (стаканов) из нескольких источников и сохраняет их в InMemory объектное хранилище.
Доступ к сохраненным данным реализован через API:
- REST: IMarketRestApiClient
- TCP: IMarketTcpApiClient

## Архитектурная схема

TBD

### Структура кода

- **`/src`**: Основная папка с исходным кодом
  - `/Market.ApiClient/`: Клиентская библиотека для доступа к API сервиса. Реализует REST и TCP API.
  - `/Market.ConsoleApp/`: Демонстрационное консольное приложение, использующее API клиента для доступа к хранилищу
  - `/Market.Core/`: Доменная модель и основные сущности приложения.
  - `/Market.Gateways.Tinvest/`: Шлюз для получения и сохранения рыночных данных из T-invest API
  - `/Market.Host/`: ASP.NET Web API хост плюс TCP сервер как BackgroundService
- **`/tests`**: Набор тестов
  - `/Market.Benchmarks/`: Перформанс тесты на основе NBommber
- **`/docs`**: Документация

### Key Configuration Files
- **`.editorconfig`**: Code formatting rules, null annotations, diagnostic configurations
- **`Directory.Build.props`**: Shared MSBuild properties across all projects
- **`Market.slnx`**: Main solution file (XML-based solution format)

### Ключевые технические моменты

- InMemory Object Store использует ReaderWriterLockSlim для эффективного доступа и Interlocked для сбора статистики
- Асинхронный TCP-сервер использует System.IO.Pipelines
- Парсер команд реализован с использованием MemoryPack
- 

## Инфраструктура

TBD

## Запуск и примеры использования

TBD

## Дашборды и метрики

TBD







