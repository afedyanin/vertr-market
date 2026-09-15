namespace Market.ApiClient;

public class MarketApiSettings
{
    public int AssetId { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string OtelExporterOltpEndpoint { get; set; } = "http://localhost:4317";
}
