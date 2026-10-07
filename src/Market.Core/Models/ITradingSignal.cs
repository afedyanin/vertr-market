namespace Market.Core.Models;

public interface ITradingSignal
{
    public string Signal { get; set; }
    public ushort AssetX { get; set; }
    public ushort AssetY { get; set; }
}
