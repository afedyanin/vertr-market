namespace Market.Core.Models;

public partial record TradeTick(
    DateTime Timestamp,
    decimal Price,
    uint Volume,
    ushort AssetId,
    byte Side);
