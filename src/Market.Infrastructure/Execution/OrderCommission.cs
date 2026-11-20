namespace Market.Infrastructure.Execution;

/// <summary>
/// Комиссия ордера, которую нужно отнести к сделкам ровно один раз.
/// </summary>
/// <remarks>
/// T-Invest сообщает комиссию на весь ордер в <c>PostOrderResponse</c>, а
/// сделки приходят отдельными сообщениями. Этот класс гарантирует, что
/// комиссия будет учтена только в одной порции сделок, даже если ответ на
/// <c>PostOrder</c> и поток сделок обрабатываются параллельно.
/// </remarks>
public sealed class OrderCommission
{
    private int _applied;

    public OrderCommission(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Commission cannot be negative.");
        }

        Amount = amount;
    }

    /// <summary>Размер комиссии в валюте счёта.</summary>
    public decimal Amount { get; }

    /// <summary>
    /// Возвращает комиссию при первом вызове и <see langword="false"/> при
    /// повторных. Нулевая комиссия не выдаётся никогда.
    /// </summary>
    public bool TryTake(out decimal commission)
    {
        if (Amount <= 0m || Interlocked.Exchange(ref _applied, 1) != 0)
        {
            commission = 0m;
            return false;
        }

        commission = Amount;
        return true;
    }
}
