namespace Market.Core.Models;

public interface ITradingSignal
{
    public double Beta { get; set; }

    public string Signal { get; set; }

    public ushort AssetX { get; set; }

    public ushort AssetY { get; set; }
}
