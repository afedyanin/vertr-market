namespace Vertr.Market.Application.Abstractions;

public interface IResetable<T> where T : class
{
    bool IsEmpty { get; set; }
    void CopyFrom(T source);
    void Reset();
}
