using Vertr.Common.Contracts;
using Vertr.Common.Contracts.Abstractions;

namespace Vertr.Market.Application.LocalStorage;

internal sealed class InstrumentsLocalStorage : IInstrumentsLocalStorage
{
    private readonly Dictionary<Guid, Instrument> _instrumentsDict = [];

    public Instrument[] GetAll()
        => [.. _instrumentsDict.Values];

    public Instrument? GetById(Guid instrumentId)
    {
        _instrumentsDict.TryGetValue(instrumentId, out var instrument);

        return instrument;
    }

    public void Load(Instrument[] instruments)
    {
        foreach (var instrument in instruments)
        {
            _instrumentsDict[instrument.Id] = instrument;
        }
    }
}
