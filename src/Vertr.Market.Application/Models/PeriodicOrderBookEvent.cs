namespace Vertr.Market.Application.Models;

public class PeriodicOrderBookEvent
{
    public DateTime TimeUtc { get; set; }

    // TODO: Refactor this
    public int[] OrderBooks { get; set; } = [];
}
