namespace Vertr.Market.Application.Models;

public class PeriodicBarEvent
{
    public DateTime TimeUtc { get; set; }

    public Bar[] Bars { get; set; } = [];
}