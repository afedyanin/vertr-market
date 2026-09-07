using System.Buffers;
using System.IO.Pipelines;
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Internals;
using Market.Core.Abstractions;
using Market.Core.Models;
using Market.Core.Tcp.Commands;
using Microsoft.Extensions.Logging;

namespace Market.Core.Tcp;

public class TcpCommandParser : IDisposable
{
    private readonly TcpResponseWriter _responseWriter;
    private readonly CommandFactory _commandFactory;
    private readonly ILogger<TcpCommandParser> _logger;

    private bool _disposed;

    public long CommandsProcessed { get; private set; }

    public TcpCommandParser(
        IObjectStore<MarketDepth> booksStore,
        PipeWriter writer,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TcpCommandParser>();

        _responseWriter = new TcpResponseWriter(
            writer,
            new MessageProtocol(),
            loggerFactory.CreateLogger<TcpResponseWriter>());

        _commandFactory = new CommandFactory(booksStore);
    }

    public async Task ReadPipeAsync(PipeReader reader, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start reading pipe.");
        CommandsProcessed = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            ReadResult result = await reader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = result.Buffer;

            while (TryReadPacket(ref buffer, out short commandId, out int correlationId, out ReadOnlySequence<byte> payload))
            {
                CommandsProcessed++;

                // Sequential on purpose: the response writer (PipeWriter) is not thread-safe for
                // concurrent GetMemory/Advance/FlushAsync, and awaiting here also guarantees every
                // response is flushed before ReadPipeAsync returns and the connection is torn down.
                //
                // LIFETIME CONTRACT: `payload` is a slice of the pipe buffer, which is released
                // by AdvanceTo below. Commands MUST deserialize it synchronously before their
                // first await — never stash the sequence for later use.
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

        _logger.LogInformation("End reading pipe.");
    }

    private static bool TryReadPacket(
        ref ReadOnlySequence<byte> buffer,
        out short commandId,
        out int correlationId,
        out ReadOnlySequence<byte> payload)
    {
        commandId = 0;
        correlationId = 0;
        payload = default;

        if (buffer.Length < TcpConsts.MessageHeaderSize)
        {
            return false;
        }

        SequenceReader<byte> reader = new SequenceReader<byte>(buffer);
        reader.TryReadBigEndian(out int packetLength);

        // A valid packet is always at least as long as its 10-byte header. Declaring a smaller
        // length would produce a negative payload length and a crash (new byte[negative]); treat it
        // as malformed and stop parsing (the connection drops when the peer closes).
        if (packetLength < TcpConsts.MessageHeaderSize)
        {
            return false;
        }

        if (buffer.Length < packetLength)
        {
            return false;
        }

        reader.TryReadBigEndian(out commandId);
        reader.TryReadBigEndian(out correlationId);

        int payloadLength = packetLength - TcpConsts.MessageHeaderSize;

        // No copy: hand the caller a slice of the pipe buffer. Deserialization reads it
        // synchronously before the pipe advances and releases the underlying arrays.
        payload = buffer.Slice(reader.Position, payloadLength);

        buffer = buffer.Slice(buffer.GetPosition(packetLength));
        return true;
    }

    private async Task ExecuteCommandAsync(
        CommandType commandType,
        int correlationId,
        ReadOnlySequence<byte> payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = _commandFactory.CreateCommand(commandType);

            if (command == null)
            {
                _logger.LogWarning("Cannot execute command type: {Command}", commandType);
                return;
            }

            await command.ExecuteAsync(
                _responseWriter,
                correlationId,
                payload,
                cancellationToken);
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

/// <summary>
/// Deserializes MemoryPack payloads read from the pipe without an extra buffer copy.
/// </summary>
public static class TcpPayload
{
    /// <summary>
    /// The sequence is a slice of pipe memory: it is only valid until the reader advances.
    /// Deserialization here is fully synchronous, which keeps it inside that lifetime.
    /// </summary>
    public static T? Deserialize<T>(ReadOnlySequence<byte> payload)
        where T : class
        => payload.IsSingleSegment
            ? MemoryPack.MemoryPackSerializer.Deserialize<T>(payload.First.Span)
            : DeserializeContiguous<T>(payload);

    private static T? DeserializeContiguous<T>(ReadOnlySequence<byte> payload)
        where T : class
    {
        // Multi-segment payloads cannot be deserialized in place; this is the only path
        // that allocates a copy.
        byte[] buffer = new byte[payload.Length];
        payload.CopyTo(buffer);
        return MemoryPack.MemoryPackSerializer.Deserialize<T>(buffer);
    }
}
