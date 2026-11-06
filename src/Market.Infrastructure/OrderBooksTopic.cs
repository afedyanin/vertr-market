
using System.Collections.Immutable;
using System.Threading.Channels;
using Market.Core.Models;
using Microsoft.Extensions.Logging;

namespace Market.Infrastructure;

public class OrderBooksTopic
{
    private readonly Channel<OrderBookDepth> _incomingChannel;
    private ImmutableArray<Channel<OrderBookDepth>> _subscribers = ImmutableArray<Channel<OrderBookDepth>>.Empty;
    private readonly object _lock = new();
    private bool _isCompleted;
    private readonly ILogger<OrderBooksTopic> _logger;

    public OrderBooksTopic(
        Channel<OrderBookDepth> incomingChannel,
        ILoggerFactory loggerFactory)
    {
        _incomingChannel = incomingChannel;
        _logger = loggerFactory.CreateLogger<OrderBooksTopic>();
    }

    public ChannelWriter<OrderBookDepth> Writer => _incomingChannel.Writer;

    public ChannelReader<OrderBookDepth> Subscribe()
    {
        lock (_lock)
        {
            if (_isCompleted)
            {
                throw new InvalidOperationException("Topic already completed.");
            }

            var subChannel = Channel.CreateUnbounded<OrderBookDepth>();
            _subscribers = _subscribers.Add(subChannel);

            return subChannel.Reader;
        }
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var book in _incomingChannel.Reader.ReadAllAsync(cancellationToken))
            {
                var currentSubs = _subscribers;

                if (currentSubs.IsEmpty)
                {
                    continue;
                }

                for (int i = 0; i < currentSubs.Length; i++)
                {
                    try
                    {
                        currentSubs[i].Writer.TryWrite(book);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to write to subscriber. Exception={Message}", ex.Message);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Topic completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in routing loop: Exception={Message}", ex.Message);
        }
        finally
        {
            CompleteAllSubscribers();
        }
    }

    private void CompleteAllSubscribers()
    {
        lock (_lock)
        {
            if (_isCompleted)
            {
                return;
            }

            _isCompleted = true;

            var currentSubs = _subscribers;
            _subscribers = ImmutableArray<Channel<OrderBookDepth>>.Empty;

            foreach (var sub in currentSubs)
            {
                sub.Writer.TryComplete();
            }
        }
    }
}
