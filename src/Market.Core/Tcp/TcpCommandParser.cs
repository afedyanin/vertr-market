using System.Buffers;
using System.IO.Pipelines;
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Internals;
using Market.Core.Tcp.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Market.Core.Tcp;

public class TcpCommandParser : IDisposable
{
    private readonly TcpResponseWriter _responseWriter;
    private readonly IServiceScope _serviceScope;
    private readonly ILogger<TcpCommandParser> _logger;

    private bool _disposed;

    public TcpCommandParser(IServiceScope serviceScope, PipeWriter writer)
    {
        _serviceScope = serviceScope;
        _logger = _serviceScope.ServiceProvider.GetRequiredService<ILogger<TcpCommandParser>>();

        _responseWriter = new TcpResponseWriter(
            writer,
            new MessageProtocol(),
            _serviceScope.ServiceProvider.GetRequiredService<ILogger<TcpResponseWriter>>());
    }

    public async Task ReadPipeAsync(PipeReader reader, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Start reading pipe.");

        while (!cancellationToken.IsCancellationRequested)
        {
            ReadResult result = await reader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = result.Buffer;

            while (TryReadPacket(ref buffer, out short commandId, out int correlationId, out byte[] payload))
            {
                _logger.LogDebug("Command received. CommandId={CommandId} CorrelationId={CorrelationId}.", commandId, correlationId);

                await ExecuteCommandAsync(
                    (CommandType)commandId,
                    correlationId,
                    payload,
                    cancellationToken);
            }

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        _logger.LogDebug("End reading pipe.");
    }

    private static bool TryReadPacket(
        ref ReadOnlySequence<byte> buffer,
        out short commandId,
        out int correlationId,
        out byte[] payload)
    {
        commandId = 0;
        correlationId = 0;
        payload = [];

        if (buffer.Length < TcpConsts.MessageHeaderSize)
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

        int payloadLength = packetLength - TcpConsts.MessageHeaderSize;
        payload = new byte[payloadLength];

        var payloadSequence = buffer.Slice(reader.Position, payloadLength);
        payloadSequence.CopyTo(payload);

        buffer = buffer.Slice(buffer.GetPosition(packetLength));
        return true;
    }

    private async Task ExecuteCommandAsync(
        CommandType commandType,
        int correlationId,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = CommandFactory.CreateCommand(commandType, _serviceScope, _responseWriter);

            if (command == null)
            {
                _logger.LogWarning("Cannot execute command type: {Command}", commandType);
                return;
            }

            _logger.LogDebug("Executing command CommandType={Command} CorrelationId={CorrelationId}.", command.CommandType, correlationId);
            await command.ExecuteAsync(correlationId, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {Command}: {Message}", commandType, ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _responseWriter?.Dispose();
        _disposed = true;
    }
}