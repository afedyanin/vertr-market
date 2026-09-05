using System.IO.Pipelines;
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Internals;
using Microsoft.Extensions.Logging;

namespace Market.Core.Tcp;

internal sealed class TcpResponseWriter : IDisposable
{
    private readonly IMessageProtocol _protocol;

    private readonly PipeWriter _writer;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private bool _disposed;

    private readonly ILogger<TcpResponseWriter> _logger;


    public TcpResponseWriter(
        PipeWriter writer,
        IMessageProtocol protocol,
        ILogger<TcpResponseWriter> logger)
    {
        _writer = writer;
        _protocol = protocol;
        _logger = logger;
    }

    public async Task WriteAsync(
        CommandType commandType,
        int totalLength,
        int correlationId,
        byte[] responsePayload,
        CancellationToken cancellationToken = default)
    {
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            byte[] packet = _protocol.Serialize(commandType, correlationId, responsePayload);

            Memory<byte> buffer = _writer.GetMemory(packet.Length);
            packet.CopyTo(buffer);

            _writer.Advance(totalLength);
            await _writer.FlushAsync(cancellationToken);

            _logger.LogInformation("Writing response: Command={Command} CorrelationId={CorrelationId} TotalLength={TotalLength}",
                commandType,
                correlationId,
                totalLength);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing response. Command={Command} CorrelationId={CorrelationId} Message={Message}",
                commandType,
                correlationId,
                ex.Message);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writeSemaphore?.Dispose();
        _disposed = true;
    }
}
