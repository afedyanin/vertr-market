using System.IO.Pipelines;
using Market.ApiClient.Tcp;

namespace Market.Core.Tcp;

internal sealed class TcpResponseWriter : IDisposable
{
    private readonly PipeWriter _writer;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);

    private bool _disposed;

    public TcpResponseWriter(PipeWriter writer)
    {
        _writer = writer;
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
            Memory<byte> buffer = _writer.GetMemory(totalLength);
            using var ms = new MemoryStream(buffer.ToArray());
            using var binaryWriter = new BinaryWriter(ms);

            binaryWriter.Write(System.Net.IPAddress.HostToNetworkOrder(totalLength));
            binaryWriter.Write(System.Net.IPAddress.HostToNetworkOrder((short)commandType));
            binaryWriter.Write(System.Net.IPAddress.HostToNetworkOrder(correlationId));
            binaryWriter.Write(responsePayload);

            _writer.Advance(totalLength);
            await _writer.FlushAsync(cancellationToken);
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
