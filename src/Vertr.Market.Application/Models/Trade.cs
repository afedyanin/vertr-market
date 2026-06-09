namespace Vertr.Market.Application.Models;

// Входной трейд (размер: 32 байта, readonly структура)
public readonly record struct Trade(
    int AssetId,
    decimal Price,
    decimal Volume,
    DateTime Timestamp
);
