using Market.ApiClient.Tcp;

namespace Market.ApiClient;

public interface ITcpClientConnection : IDisposable
{
    event EventHandler? OnConnected;

    event EventHandler? OnDisconnected;

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task<byte[]> SendRequestAsync<TRequest>(CommandType command, TRequest dto);
}
