﻿using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FlaneerMediaLib.Logging;
using FlaneerMediaLib.VideoDataTypes;

namespace FlaneerMediaLib;

/// <summary>
/// Video sink that broadcasts frames over UDP
/// </summary>
public class UDPVideoSink : IVideoSink
{
    private IEncoder encoder = null!;
    private IVideoSource videoSource = null!;

    private readonly Socket s = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    private readonly IPAddress broadcast;
    private readonly int port;
    private UInt32 nextFrame = 0;
    
    private Logger logger;

    /// <summary>
    /// ctor
    /// </summary>
    public UDPVideoSink()
    {
        logger = Logger.GetLogger(this);
        ServiceRegistry.TryGetService<CommandLineArgumentStore>(out var clas);
        var frameSettings = clas.GetParams(CommandLineArgs.BroadcastAddress);
        
        broadcast = IPAddress.Parse(frameSettings[0]);
        port = Int32.Parse(frameSettings[1]);
        s.SendBufferSize = Int16.MaxValue - VideoUtils.UDPHEADERSIZE;
        GetEncoder();
        GetSource();
    }

    private void GetEncoder()
    {
        if (ServiceRegistry.TryGetService<IEncoder>(out var foundEncoder))
        {
            encoder = foundEncoder;
        }
        else
        {
            ServiceRegistry.ServiceAdded += service =>
            {
                if (service is IEncoder encoderService)
                    encoder = encoderService;
            };
        }
    }
    
    private void GetSource()
    {
        if (ServiceRegistry.TryGetService<IVideoSource>(out var foundSource))
        {
            videoSource = foundSource;
        }
        else
        {
            ServiceRegistry.ServiceAdded += service =>
            {
                if (service is IVideoSource sourceService)
                    videoSource = sourceService;
            };
        }
    }
    
    /// <inheritdoc />
    public void ProcessFrame() => CaptureFrameImpl();
    /// <inheritdoc />
    public void ProcessFrames(int numberOfFrames, int targetFramerate) => CaptureFrameImpl(numberOfFrames, targetFramerate);

    private void CaptureFrameImpl(int numberOfFrames = 1, int targetFramerate = -1)
    {
        //Return in the case the encoder is not created
        if(encoder == default! || videoSource == default!)
            return;
        
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();
        var frameTime = targetFramerate == -1 ? new TimeSpan(0) : new TimeSpan(0, 0, 0, 0, 1000 / targetFramerate);

        for (int i = 0; i < numberOfFrames; i++)
        {
            while (stopWatch.Elapsed < (frameTime*i))
            {
                Thread.Sleep(1);
            }

            try
            {
                var frame = encoder.GetFrame();
                if(frame is UnmanagedVideoFrame unmanagedFrame)
                    SendFrame(unmanagedFrame, frameTime);
            }
            catch (Exception)
            {
                // ignored
            }
        }
        stopWatch.Stop();
    }

    private int sentPacketCount = 0;
    private unsafe void SendFrame(UnmanagedVideoFrame frame, TimeSpan frameTime)
    {
        using var uStream = new UnmanagedMemoryStream((byte*) frame.FrameData, frame.FrameSize);
        var frameBytes = new byte[frame.FrameSize];
        uStream.Read(frameBytes, 0, frame.FrameSize);
            
        var frameWritableSize = Int16.MaxValue - VideoUtils.UDPHEADERSIZE;
        var numberOfPackets = (byte) Math.Ceiling((double)frame.FrameSize / frameWritableSize);
            
        var ep = new IPEndPoint(broadcast, port);
        var sent = 0;
        for (byte i = 0; i < numberOfPackets; i++)
        {
            var frameHeader = new TransmissionVideoFrame
            {
                Width = (short) videoSource.FrameSettings.Width,
                Height = (short) videoSource.FrameSettings.Height,
                NumberOfPackets = numberOfPackets,
                PacketIdx = i,
                FrameDataSize = frame.FrameSize,
                SequenceIDX = nextFrame
            };
                
            var headerBytes = frameHeader.ToUDPPacket();

            var packetSize = Math.Min(frameWritableSize, frame.FrameSize - sent);
            byte[] transmissionArray = new byte[headerBytes.Length + packetSize];

            frameHeader.PacketSize = (ushort) transmissionArray.Length;
            
            Array.Copy(headerBytes, transmissionArray, headerBytes.Length);
            Array.Copy(frameBytes, sent, transmissionArray, headerBytes.Length, packetSize);
                
            s.SendTo(transmissionArray, ep);

            sent += packetSize;
            logger.Debug($"SENT CHUNK OF {nextFrame} | {sent} / {frame.FrameSize} [gray]Total Sent Packets = {sentPacketCount++}[/]");
            
            Thread.Sleep(frameTime/numberOfPackets);
        }
        nextFrame++;
    }
}