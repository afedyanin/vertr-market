using MessagePack;

namespace Market.Core.Models;

[MessagePackObject]

public record Quote
{
    [Key(0)]
    public double Bid { get; init; }

    [Key(1)]
    public double Ask { get; init; }

    [IgnoreMember]
    public double Mid => Bid + Ask / 2.0;

    public Quote(double bid, double ask)
    {
        Bid = bid;
        Ask = ask;
    }
}
