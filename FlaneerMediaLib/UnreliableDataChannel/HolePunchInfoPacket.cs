﻿using System.Net;
using System.Text;
using Microsoft.VisualBasic.CompilerServices;

namespace FlaneerMediaLib.UnreliableDataChannel;

/// <summary>
/// A packet containing an ip address for facilitating hole punching
/// </summary>
public class HolePunchInfoPacket : IPacketInfo
{
    /// <summary>
    /// The size of the header in bytes
    /// <remarks>This is manually calculated</remarks>
    /// </summary>
    public const int HeaderSize = 21;
    
    /// <inheritdoc/>
    public PacketType PacketType => PacketType.HolePunchInfo;
    /// <inheritdoc/>
    public ushort PacketSize { get; set; }
    /// <inheritdoc/>
    public long TimeStamp { get; init; }
    /// <inheritdoc/>
    public uint PacketId { get; init; }

    /// <summary>
    /// The IP address serialised into a single int
    /// </summary>
    private UInt32 host;
    /// <summary>
    /// The port of the client
    /// </summary>
    private UInt16 port;
    
    /// <inheritdoc/>
    public byte[] ToUDPPacket()
    {
        byte[] ret = new byte[HeaderSize];
        using MemoryStream stream = new MemoryStream(ret);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, false);
        writer.Write((byte) PacketType);
        writer.Write(PacketSize);
        writer.Write(DateTime.UtcNow.Ticks);
        writer.Write(PacketId);
        writer.Write(host);
        writer.Write(port);
        return ret;
    }
    
    /// <summary>
    /// Helper method for turning UDP packet into info packet
    /// </summary>
    public static HolePunchInfoPacket FromBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, 0, HeaderSize);
        using var reader = new BinaryReader(stream, Encoding.UTF8, false);
        
        var packetType = reader.ReadByte();
        if (packetType != (byte)PacketType.HolePunchInfo)
        {
            throw new Exception($"Trying to decode a {(PacketType) packetType} as a {PacketType.HolePunchInfo}");
        }
        
        var ret = new HolePunchInfoPacket()
        {
            PacketSize = reader.ReadUInt16(),
            TimeStamp = reader.ReadInt64(),
            PacketId = reader.ReadUInt32(),
            host = reader.ReadUInt32(),
            port = reader.ReadUInt16(),
        };

        return ret;
    }

    /// <summary>
    /// Helper method for turning ip endpoint into info packet
    /// </summary>
    public static HolePunchInfoPacket FromIpEndpoint(IPEndPoint ep)
    {
        return new HolePunchInfoPacket()
        {
            host = IpToUInt32(ep.Address.ToString()),
            port = (UInt16) ep.Port
        };
    }

    
    /// <summary>
    /// Helper method for turning info packet into ip endpoint
    /// </summary>
    public IPEndPoint? ToEndPoint() => new IPEndPoint(new IPAddress(host), port);

    private static UInt32 IpToUInt32(string ip)
    {
        if (!IPAddress.TryParse(ip, out IPAddress address)) return 0;
        return BitConverter.ToUInt32(address.GetAddressBytes(), 0);
    }
    
    /// <inheritdoc/>
    public override string ToString() => $"{new IPAddress(host)}:{port}";
}