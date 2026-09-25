namespace Market.Contracts.Models;

public partial record TradeTick(
    DateTime Timestamp,
    decimal Price,
    uint Volume,
    ushort AssetId,
    byte Side);
