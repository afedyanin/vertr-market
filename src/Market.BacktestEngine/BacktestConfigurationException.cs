namespace Market.BacktestEngine;

/// <summary>
/// Ошибка конфигурации или запуска бэктеста.
/// </summary>
public sealed class BacktestConfigurationException : Exception
{
    public BacktestConfigurationException(string message)
        : base(message)
    {
    }

    public BacktestConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
