using System.IO.Pipelines;
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Internals;
using Microsoft.Extensions.Logging;

namespace Market.Core.Tcp;

public sealed class TcpResponseWriter : IDisposable
{
    private readonly IMessageProtocol _protocol;
    private readonly PipeWriter _writer;
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
        try
        {
            // Write header + payload straight into the pipe buffer. This avoids allocating a
            // separate packet array and copying it; the only buffer is the pipe's own (reused).
            Memory<byte> memory = _writer.GetMemory(totalLength);
            _protocol.WriteHeader(memory.Span[..TcpConsts.MessageHeaderSize], commandType, correlationId, totalLength);
            responsePayload.AsSpan().CopyTo(memory.Span[TcpConsts.MessageHeaderSize..]);

            _writer.Advance(totalLength);
            await _writer.FlushAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Writing response: Command={Command} CorrelationId={CorrelationId} TotalLength={TotalLength}",
                    commandType,
                    correlationId,
                    totalLength);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing response. Command={Command} CorrelationId={CorrelationId} Message={Message}",
                commandType,
                correlationId,
                ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
