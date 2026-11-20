namespace Market.Infrastructure.Execution;

/// <summary>
/// Проверка пары объёмов перед отправкой ордеров.
/// </summary>
public static class PairSignalValidator
{
    /// <summary>
    /// Пара валидна, если обе ноги ненулевые и противоположны по знаку.
    /// Нулевая или однонаправленная пара — это не парный ордер, а направленная
    /// позиция, поэтому такой сигнал нужно отклонить.
    /// </summary>
    public static bool TryValidate(int xQtyLots, int yQtyLots, out string error)
    {
        if (xQtyLots == 0 || yQtyLots == 0)
        {
            error = "Pair quantities must be non-zero.";
            return false;
        }

        if (Math.Sign(xQtyLots) == Math.Sign(yQtyLots))
        {
            error = "Pair quantities must have opposite signs.";
            return false;
        }

        // Math.Abs(int.MinValue) бросает OverflowException.
        if (xQtyLots == int.MinValue || yQtyLots == int.MinValue)
        {
            error = "Pair quantities are out of supported range.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
