using System.Buffers;
using System.IO.Pipelines;
using Market.ApiClient.Dtos;

namespace Market.Core.Tcp;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public class TcpCommandParser
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private const int HeaderSize = 10; // Length(4) + Cmd(2) + CorId(4)
    private readonly PipeWriter _writer;

    // Семафор для защиты PipeWriter от одновременной записи из параллельных задач
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);

    public TcpCommandParser(PipeWriter writer)
    {
        _writer = writer;
    }

    public async Task ReadPipeAsync(PipeReader reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ReadResult result = await reader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = result.Buffer;

            // Извлекаем ВСЕ доступные на данный момент полные пакеты из буфера
            while (TryReadPacket(ref buffer, out short commandId, out int correlationId, out byte[] payload))
            {
                // КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: Запускаем обработку команды в ThreadPool, НЕ дожидаясь её завершения.
                // Цикл мгновенно переходит к чтению следующего пакета из сети.
                _ = Task.Run(() => ExecuteCommandParallelAsync(commandId, correlationId, payload, cancellationToken), cancellationToken);
            }

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }
    }

    private bool TryReadPacket(ref ReadOnlySequence<byte> buffer, out short commandId, out int correlationId, out byte[] payload)
    {
        commandId = 0;
        correlationId = 0;
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        payload = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        if (buffer.Length < HeaderSize)
        {
            return false;
        }

        SequenceReader<byte> reader = new SequenceReader<byte>(buffer);
        reader.TryReadBigEndian(out int packetLength);

        if (buffer.Length < packetLength)
        {
            return false;
        }

        reader.TryReadBigEndian(out commandId);
        reader.TryReadBigEndian(out correlationId);

        int payloadLength = packetLength - HeaderSize;
        payload = new byte[payloadLength];

        var payloadSequence = buffer.Slice(reader.Position, payloadLength);
        payloadSequence.CopyTo(payload);

        buffer = buffer.Slice(buffer.GetPosition(packetLength));
        return true;
    }

    /// <summary>
    /// Этот метод выполняется изолированно в отдельном потоке для каждого пакета
    /// </summary>
    private async Task ExecuteCommandParallelAsync(
        short commandId,
        int correlationId,
        byte[] payload,
        CancellationToken ct)
    {
        try
        {
            var command = (CommandType)commandId;

            switch (command)
            {
                case CommandType.GetBooksRequest:
                    // Тяжелый метод с обращением к логике
                    await HandleGetBooksRequestAsync(correlationId, payload, ct);
                    break;

                case CommandType.PostBooks:
                    // Даже методы без ответа выполняются параллельно, не тормозя сеть
                    HandlePostBooks(payload);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Важно ловить ошибки здесь, иначе необработанное исключение в Task.Run повалит процесс
            Console.WriteLine($"[Ошибка] Сбой при параллельном выполнении команды {commandId}: {ex.Message}");
        }
    }

    private async Task HandleGetBooksRequestAsync(int correlationId, byte[] payload, CancellationToken ct)
    {
        // 1. Десериализация (быстрая, CPU-bound)
        //var request = MemoryPack.MemoryPackSerializer.Deserialize<GetBooksRequestDto>(payload);

        // 2. Имитация долгого запроса (например, тяжелый I/O к БД на 500мс)
        // В этот момент поток чтения сокета РАБОТАЕТ и принимает другие команды!
        await Task.Delay(500, ct);

        //var mockResult = new MarketDepthDto[] { new MarketDepthDto { AssetId = request.AssetId, Price = 100.5m, Volume = 10 } };
        var mockResult = Array.Empty<MarketDepthDto>();

        // 3. Сериализация ответа
        byte[] responsePayload = MemoryPack.MemoryPackSerializer.Serialize(mockResult);
        int totalLength = HeaderSize + responsePayload.Length;

        // 4. Безопасная отправка через семафор
        await _writeSemaphore.WaitAsync(ct);
        try
        {
            Memory<byte> buffer = _writer.GetMemory(totalLength);
            using (var ms = new MemoryStream(buffer.ToArray()))
            using (var binaryWriter = new BinaryWriter(ms))
            {
                binaryWriter.Write(System.Net.IPAddress.HostToNetworkOrder(totalLength));
                binaryWriter.Write(System.Net.IPAddress.HostToNetworkOrder((short)CommandType.GetBooksResponse));
                binaryWriter.Write(System.Net.IPAddress.HostToNetworkOrder(correlationId));
                binaryWriter.Write(responsePayload);
            }

            _writer.Advance(totalLength);
            await _writer.FlushAsync(ct);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    private void HandlePostBooks(byte[] payload)
    {
        //var books = MemoryPack.MemoryPackSerializer.Deserialize<MarketDepthDto[]>(payload);
        // Бизнес-логика...
    }
}