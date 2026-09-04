using System.Buffers;
using MemoryPack;

namespace Market.ApiClient.Tcp.Internals;

internal interface IMessageProtocol
{
    byte[] Serialize<TRequest>(CommandType command, int correlationId, TRequest dto);
    bool TryParsePacket(ref ReadOnlySequence<byte> buffer, out RawPacket packet);
}

internal readonly record struct RawPacket(short CommandId, int CorrelationId, byte[] Payload);

internal sealed class MessageProtocol : IMessageProtocol
{
    public byte[] Serialize<TRequest>(CommandType command, int correlationId, TRequest dto)
    {
        byte[] payload = MemoryPackSerializer.Serialize(dto);
        int totalLength = TcpConsts.MessageHeaderSize + payload.Length;

        byte[] packet = new byte[totalLength];
        using var ms = new MemoryStream(packet);
        using var writer = new BinaryWriter(ms);

        writer.Write(System.Net.IPAddress.HostToNetworkOrder(totalLength));
        writer.Write(System.Net.IPAddress.HostToNetworkOrder((short)command));
        writer.Write(System.Net.IPAddress.HostToNetworkOrder(correlationId));
        writer.Write(payload);

        return packet;
    }

    public bool TryParsePacket(ref ReadOnlySequence<byte> buffer, out RawPacket packet)
    {
        packet = default;
        if (buffer.Length < TcpConsts.MessageHeaderSize)
        {
            return false;
        }

        var seqReader = new SequenceReader<byte>(buffer);
        seqReader.TryReadBigEndian(out int packetLength);

        if (buffer.Length < packetLength)
        {
            return false;
        }

        seqReader.TryReadBigEndian(out short commandId);
        seqReader.TryReadBigEndian(out int correlationId);

        int payloadLength = packetLength - TcpConsts.MessageHeaderSize;
        byte[] payload = new byte[payloadLength];
        buffer.Slice(seqReader.Position, payloadLength).CopyTo(payload);

        // Сдвигаем буфер на прочитанную длину пакета
        buffer = buffer.Slice(buffer.GetPosition(packetLength));

        packet = new RawPacket(commandId, correlationId, payload);
        return true;
    }
}
