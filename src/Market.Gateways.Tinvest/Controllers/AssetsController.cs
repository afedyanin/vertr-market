using Microsoft.AspNetCore.Mvc;
using Tinkoff.InvestApi;

namespace Market.Gateways.Tinvest.Controllers;

[Route("api/assets")]
[ApiController]
public class AssetsController : ControllerBase
{
    private readonly InvestApiClient _apiClient;

    public AssetsController(InvestApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("find")]
    public async Task<IActionResult> FindInstrument(string query)
    {
        var request = new Tinkoff.InvestApi.V1.FindInstrumentRequest
        {
            Query = query,
            ApiTradeAvailableFlag = true,
        };

        var response = await _apiClient.Instruments.FindInstrumentAsync(request);

        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpGet("by-ticker/{classCode}/{ticker}")]
    public async Task<IActionResult> GetInstrumentByTicker(string classCode, string ticker, bool rawData = false)
    {
        var request = new Tinkoff.InvestApi.V1.InstrumentRequest
        {
            ClassCode = classCode,
            Id = ticker,
            IdType = Tinkoff.InvestApi.V1.InstrumentIdType.Ticker,
        };

        var response = await _apiClient.Instruments.GetInstrumentByAsync(request);

        if (response is null || response.Instrument is null)
        {
            return NotFound();
        }

        if (rawData)
        {
            return Ok(response);
        }

        var asset = TinvestMapper.ToAsset(response.Instrument);

        return Ok(asset);
    }


    [HttpGet("share-by-id/{shareId:guid}")]
    public async Task<IActionResult> GetShareById(Guid shareId, bool rawData = false)
    {
        var request = new Tinkoff.InvestApi.V1.InstrumentRequest
        {
            Id = shareId.ToString(),
            IdType = Tinkoff.InvestApi.V1.InstrumentIdType.Uid,
        };

        var response = await _apiClient.Instruments.ShareByAsync(request);

        if (response is null || response.Instrument is null)
        {
            return NotFound();
        }

        if (rawData)
        {
            return Ok(response);
        }

        var asset = TinvestMapper.ToAsset(response.Instrument);

        return Ok(asset);
    }

    [HttpGet("future-by-id/{futureId:guid}")]
    public async Task<IActionResult> GetFutureByIdRaw(Guid futureId, bool rawData = false)
    {
        var request = new Tinkoff.InvestApi.V1.InstrumentRequest
        {
            Id = futureId.ToString(),
            IdType = Tinkoff.InvestApi.V1.InstrumentIdType.Uid,
        };

        var response = await _apiClient.Instruments.FutureByAsync(request);

        if (response is null || response.Instrument is null)
        {
            return NotFound();
        }

        if (rawData)
        {
            return Ok(response);
        }

        var asset = TinvestMapper.ToAsset(response.Instrument);

        return Ok(asset);
    }
}
