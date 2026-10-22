using System.Threading.Channels;
using Grpc.Core;
using Market.Core.Models;
using Microsoft.Extensions.Options;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest.BackgroundServices;

public sealed class OrderTradesStreamService : StreamServiceBase
{
    private readonly Channel<MyTrade> _tradesChannel;
    protected override ILogger Logger { get; init; }
    protected override bool IsEnabled { get; init; }

    public OrderTradesStreamService(
        IServiceProvider serviceProvider,
        IOptions<TinvestSettings> tinvestOptions,
        ILoggerFactory loggerFactory) : base(serviceProvider, tinvestOptions, loggerFactory)
    {
        Logger = loggerFactory.CreateLogger<OrderTradesStreamService>();
        IsEnabled = _tinvestSettings.TradesStreamEnabled;

        _tradesChannel = serviceProvider.GetRequiredService<Channel<MyTrade>>();
    }

    protected override async Task Subscribe(Action onDataReceived, CancellationToken stoppingToken = default)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var investApiClient = scope.ServiceProvider.GetRequiredService<InvestApiClient>();

        var request = new TradesStreamRequest();
        request.Accounts.Add(_tinvestSettings.AccountId);

        using var stream = investApiClient.OrdersStream.TradesStream(request, headers: null, deadline: null, stoppingToken);

        var isFirstMessage = true;

        await foreach (var response in stream.ResponseStream.ReadAllAsync(stoppingToken))
        {
            // Если пришло любое сообщение из сети, сбрасываем экспоненциальный таймер в основном цикле
            if (isFirstMessage)
            {
                onDataReceived();
                isFirstMessage = false;
            }

            if (response.PayloadCase == TradesStreamResponse.PayloadOneofCase.OrderTrades)
            {
                Logger.LogInformation($"New order trades received for OrderId={response.OrderTrades.OrderId} TradesCount={response.OrderTrades.Trades.Count}");

                foreach (var trade in TinvestMapper.ToMyTrades(response.OrderTrades))
                {
                    _tradesChannel.Writer.TryWrite(trade);
                }
            }

            if (response.PayloadCase == TradesStreamResponse.PayloadOneofCase.Ping)
            {
                Logger.LogDebug($"Trades ping received: {response.Ping}");
                continue;
            }

            if (response.PayloadCase == TradesStreamResponse.PayloadOneofCase.Subscription)
            {
                Logger.LogInformation($"Trades subscription received: {response.Subscription}");
                continue;
            }
        }
    }

    protected override ValueTask OnComplete()
    {
        _tradesChannel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
