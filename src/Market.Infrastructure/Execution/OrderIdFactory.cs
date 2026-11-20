using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Market.Infrastructure.Execution;

/// <summary>
/// Детерминированные client order id (idempotency keys) для T-Invest.
/// </summary>
/// <remarks>
/// Повторная отправка того же сигнала (рестарт, повторная доставка из канала)
/// даёт тот же <c>OrderId</c>, поэтому биржа не создаст дубликат позиции.
/// </remarks>
public static class OrderIdFactory
{
    /// <summary>Ограничение T-Invest на длину <c>OrderId</c>.</summary>
    public const int MaxLength = 36;

    /// <summary>
    /// Создаёт idempotency key для ноги парного ордера.
    /// </summary>
    public static string Create(Guid strategyId, DateTime timestamp, ushort assetId, int qtyLots)
    {
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{strategyId:N}|{timestamp.Ticks}|{assetId}|{qtyLots}");

        return Hash(payload);
    }

    /// <summary>
    /// Создаёт idempotency key для компенсирующего (закрывающего) ордера.
    /// </summary>
    public static string CreateCompensation(string originalOrderId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalOrderId);
        return Hash($"compensate|{originalOrderId}");
    }

    private static string Hash(string payload)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(payload), hash);

        // 16 байт хеша в виде GUID — ровно 36 символов, как требует API.
        return new Guid(hash[..16]).ToString("D");
    }
}
