namespace Market.Core.Tcp;

internal static class TcpConsts
{
    public const int MessageHeaderSize = 10; // Length(4) + Cmd(2) + CorId(4)
}
