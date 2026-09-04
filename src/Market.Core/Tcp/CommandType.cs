namespace Market.Core.Tcp;

public enum CommandType : short
{
    Undefined = 0,
    PostBooks = 1,
    GetBooksRequest = 2,
    GetBooksResponse = 3,
    DeleteBooksByAsset = 4,
    // ... и так далее
}