using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
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
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _commandCounter;
    private readonly Histogram<double> _commandDurationHistogram;

    private bool _disposed;

    public long CommandsProcessed { get; private set; }

    public TcpCommandParser(
        IObjectStore<MarketDepth> booksStore,
        PipeWriter writer,
        ILoggerFactory loggerFactory,
        ActivitySource activitySource,
        Counter<long> commandCounter,
        Histogram<double> commandDurationHistogram
        )
    {
        _logger = loggerFactory.CreateLogger<TcpCommandParser>();

        _responseWriter = new TcpResponseWriter(
            writer,
            new MessageProtocol(),
            loggerFactory.CreateLogger<TcpResponseWriter>());

        _commandFactory = new CommandFactory(booksStore);
        _activitySource = activitySource;
        _commandCounter = commandCounter;
        _commandDurationHistogram = commandDurationHistogram;
    }

    public async Task ReadPipeAsync(PipeReader reader, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start reading pipe.");

        while (!cancellationToken.IsCancellationRequested)
        {
            ReadResult result = await reader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = result.Buffer;

            while (TryReadPacket(ref buffer, out short commandId, out int correlationId, out ReadOnlySequence<byte> payload))
            {
                var stopwatch = Stopwatch.StartNew();

                await ExecuteCommandAsync(
                    (CommandType)commandId,
                    correlationId,
                    payload,
                    cancellationToken);

                CommandsProcessed++;
                _commandCounter.Add(1);
                _commandDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds);
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

            using var activity = _activitySource.StartActivity($"Process command", ActivityKind.Server);

            if (activity is not null)
            {
                activity.SetTag("command.type", commandType);
                activity.SetTag("command.correlationId", correlationId);
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
