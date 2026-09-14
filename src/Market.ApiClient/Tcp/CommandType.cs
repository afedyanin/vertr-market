namespace Market.ApiClient.Tcp;

public enum CommandType : short
{
    Undefined = 0,
    PostBooks = 1,
    GetBooksRequest = 2,
    GetBooksResponse = 3,
    DeleteBooksByAsset = 4,
    ClearBooks = 5,
    PostTrades = 6,
    GetTradesRequest = 7,
    GetTradesResponse = 8,
    DeleteTradesByAsset = 9,
    ClearTrades = 10
}