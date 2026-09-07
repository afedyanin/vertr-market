using System.Buffers;
using System.Buffers.Binary;
using MemoryPack;

namespace Market.ApiClient.Tcp.Internals;

public interface IMessageProtocol
{
    byte[] Serialize<TRequest>(CommandType command, int correlationId, TRequest dto);
    byte[] Serialize(CommandType command, int correlationId, byte[] payload);

    bool TryParsePacket(ref ReadOnlySequence<byte> buffer, out RawPacket packet);

    // Writes the 10-byte big-endian header (Length:4, Command:2, CorrelationId:4) into destination.
    void WriteHeader(Span<byte> destination, CommandType command, int correlationId, int totalLength);
}

public readonly record struct RawPacket(short CommandId, int CorrelationId, byte[] Payload);

public sealed class MessageProtocol : IMessageProtocol
{
    public byte[] Serialize<TRequest>(CommandType command, int correlationId, TRequest dto)
    {
        byte[] payload = MemoryPackSerializer.Serialize(dto);
        return Serialize(command, correlationId, payload);
    }

    public byte[] Serialize(CommandType command, int correlationId, byte[] payload)
    {
        int totalLength = TcpConsts.MessageHeaderSize + payload.Length;

        byte[] packet = new byte[totalLength];
        WriteHeader(packet.AsSpan(), command, correlationId, totalLength);
        payload.CopyTo(packet.AsSpan(TcpConsts.MessageHeaderSize));

        return packet;
    }

    public void WriteHeader(Span<byte> destination, CommandType command, int correlationId, int totalLength)
    {
        BinaryPrimitives.WriteInt32BigEndian(destination, totalLength);
        BinaryPrimitives.WriteInt16BigEndian(destination.Slice(4), (short)command);
        BinaryPrimitives.WriteInt32BigEndian(destination.Slice(6), correlationId);
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

        // A valid packet is at least as long as its 10-byte header; a smaller declared length
        // would yield a negative payload length and a crash (new byte[negative]).
        if (packetLength < TcpConsts.MessageHeaderSize)
        {
            return false;
        }

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
