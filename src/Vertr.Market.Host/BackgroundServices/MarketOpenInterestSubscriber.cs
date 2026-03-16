using StackExchange.Redis;
using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;
using static StackExchange.Redis.RedisChannel;

namespace Vertr.Market.Host.BackgroundServices;

public class MarketOpenInterestSubscriber : RedisServiceBase
{
    private readonly ITimeKeyedLocalStorage<OpenInterest> _openInterestLocalStorage;
    private readonly ILogger<MarketOpenInterestSubscriber> _logger;

    protected override RedisChannel RedisChannel => new RedisChannel(Subscriptions.OpenInterests.Channel, PatternMode.Pattern);
    protected override bool IsEnabled => Subscriptions.OpenInterests.IsEnabled;

    public MarketOpenInterestSubscriber(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
        _openInterestLocalStorage = serviceProvider.GetRequiredService<ITimeKeyedLocalStorage<OpenInterest>>();
        _logger = LoggerFactory.CreateLogger<MarketOpenInterestSubscriber>();
    }

    public override void HandleSubscription(RedisChannel channel, RedisValue message)
    {
        var openInterest = OpenInterest.FromJson(message.ToString());

        if (openInterest == null)
        {
            _logger.LogWarning("Cannot deserialize Open Interest from message={Message}", message);
            return;
        }

        if (openInterest.InstrumentId == Guid.Empty)
        {
            _logger.LogError("Open Interest with empty InstrumentId received. Message={Message}", message);
            return;
        }

        var added = _openInterestLocalStorage.Add(openInterest.InstrumentId, openInterest);
        _logger.LogDebug("Received open interest from cahnnel={Channel} Added={Added} OpenInterest={OpenInterest}", channel, added, openInterest);
    }
}
