
Для систем с ультра-низкой задержкой (Ultra-Low Latency) и архитектурой Zero-Allocation правильная настройка Garbage Collection (GC) критически важна. Даже если ваш код не выделяет память в куче, сторонние библиотеки, среда выполнения .NET или системные алокации могут спровоцировать сборку мусора, что вызовет нежелательные задержки (Stuttering/GC pauses).

В современном .NET (9/10+) настройки задаются прямо в файле проекта .csproj или в файле runtimeconfig.json.

Вот эталонный конфиг для .csproj и разбор того, как это работает в Docker/Kubernetes.

## 1. Конфигурация .csproj для Low-Latency

Добавьте следующие свойства в ваш файл проекта внутри тега <PropertyGroup>:

```cs
<PropertyGroup>
  <!-- Включаем серверный GC (Server GC). 
       Он создает отдельные потоки сборщика под каждое ядро CPU. 
       Обеспечивает максимальную пропускную способность. -->
  <ServerGarbageCollection>true</ServerGarbageCollection>

  <!-- Включаем фоновую сборку мусора (Background GC).
       Позволяет собирать объекты 0 и 1 поколений параллельно с работой вашего кода,
       минимизируя паузы "Stop-the-world". -->
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>

  <!-- Оптимизация под Docker: разрешаем GC динамически адаптироваться под лимиты контейнера -->
  <GCAsynchronousWorkerThreads>true</GCAsynchronousWorkerThreads>
  
  <!-- Фиксируем количество потоков GC. Рекомендуется выделить под GC строго определенное 
       количество ядер, оставив остальные под Disruptor и обработку сети. -->
  <!-- <GCLanes>2</GCLanes> -->
</PropertyGroup>
```

## 2. Управление GC прямо из C# кода (GC Region)

Так как Disruptor работает в бесконечном цикле, на критических участках кода (например, во время работы торговой сессии) можно временно запретить GC проводить полные сборки мусора (GC Generation 2).
Для этого в .NET есть режим GC.StartNoGCRegion.

```csharp
public static async Task Main()
{
    // ... инициализация Disruptor ...

    // Перед запуском критического цикла выделяем "буфер безопасности" в куче (например, 50 МБ)
    // Если в процессе работы будет выделено меньше 50 МБ, GC гарантированно НЕ запустится.
    long memoryBufferBytes = 50 * 1024 * 1024; 
    
    bool isNoGCStarted = GC.TryStartNoGCRegion(memoryBufferBytes);

    try
    {
        // Запуск вашего основного пайплайна агрегации трейдов
        await aggregator.ParseTradeStreamAsync(mockStream, cts.Token);
    }
    finally
    {
        // После окончания торговой сессии или при остановке приложения выходим из режима
        if (isNoGCStarted)
        {
            GC.EndNoGCRegion();
        }
    }
}
```
------------------------------

## 3. Специфика настройки в Docker и Kubernetes
Когда приложение работает в контейнере, .NET Server GC по умолчанию видит все ядра хоста, а не лимиты контейнера. Если вы ограничите контейнер в Kubernetes (например, limits.cpu: "2" на 64-ядерном сервере), .NET все равно создаст 64 потока сборщика мусора. Это приведет к жесткому троттлингу со стороны Linux CFS Scheduler (контейнер начнет зависать).
## Правила настройки для Kubernetes манифеста:

   1. Задавайте лимиты CPU целыми числами: Начиная с .NET 8/9, рантайм лучше понимает дробные CPU, но для Ultra-Low Latency систем всегда используйте целые числа (например, limits: cpu: "4").
   2. Используйте CPU pinning (рекомендуется): Если есть возможность, используйте Kubernetes Static CPU Manager, чтобы ядра были монопольно отданы вашему контейнеру. Это исключит context switch (переключение контекста ОС).
   3. Ограничивайте потоки GC через переменные окружения: Если вы даете контейнеру 4 ядра, укажите в env секции поды жесткий лимит на потоки GC, чтобы они не раздувались:

```yaml
apiVersion: apps/v1kind: Deploymentmetadata:
  name: trade-aggregatorspec:
  template:
    spec:
      containers:
      - name: aggregator-app
        image: your-registry/aggregator:latest
        resources:
          limits:
            cpu: "4"
            memory: "2Gi"
          requests:
            cpu: "4"
            memory: "2Gi"
        env:
        # Указываем .NET использовать строго 2 потока под GC, 
        # чтобы оставшиеся 2 ядра были полностью свободны под Disruptor и сеть.
        - name: DOTNET_GCLanes
          value: "2"
        # Альтернативный вариант (схожий по смыслу):限制 CPU маску
        - name: DOTNET_GCHeapCount
          value: "2"
```

Если вы хотите пойти еще дальше, можно переключиться в режим Workstation GC (<ServerGarbageCollection>false</ServerGarbageCollection>). В контексте Docker, если контейнеру выделено мало ядер (1–2 ядра), Workstation GC часто показывает себя стабильнее, так как не создает тяжелых фоновых потоков под каждое ядро.
Какое количество ядер CPU и какой объем памяти планируется выделить под один контейнер в вашей системе? Я смогу подсказать точные значения для DOTNET_GCLanes и настроек Disruptor.

