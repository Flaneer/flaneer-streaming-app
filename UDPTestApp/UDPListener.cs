﻿using FlaneerMediaLib;
using FlaneerMediaLib.VideoStreaming;

namespace UDPTestApp;

public class UDPListener
{
    private const int listenPort = 11000;

    public static void StartListener()
    {
        IVideoSource videoSourceItfce;
        ServiceRegistry.TryGetService(out videoSourceItfce);
        
        var videoSource = videoSourceItfce as UDPVideoSource;
        if (videoSource == null)
            return;

        int it = 0;

        /*int it = 0;
    while (it < 3)
    {
        try
        {
            ManagedVideoFrame frame = videoSource.GetFrame() as ManagedVideoFrame;
            if(frame.Stream.Length == 0)
                continue;

            MemoryStream outStream = new MemoryStream();

            var task = ffMpeg.ConvertLiveMedia(frame.Stream, Format.h264, outStream, Format.mjpeg, new ConvertSettings
            {
                CustomInputArgs = $"-video_size {videoSource.FrameSettings.Width}x{videoSource.FrameSettings.Height}"
            });

            task.Start();

            File.WriteAllBytes($"{it++}.jpeg", outStream.GetBuffer());
            
            Console.WriteLine("------------------------------------------------------------");
        }
        catch (SocketException e)
        {
            Console.WriteLine(e);
        }
    }*/
    }
    
    /*using (Process myProcess = new Process())
{
    myProcess.StartInfo.FileName = "ffprobe.exe";
    myProcess.StartInfo.Arguments = " -i - -select_streams v -show_frames -of csv -show_entries frame=pict_type";
    myProcess.StartInfo.UseShellExecute = false;
    myProcess.StartInfo.RedirectStandardInput = true;
    myProcess.StartInfo.RedirectStandardOutput = true;

    myProcess.Start();

    StreamWriter myStreamWriter = myProcess.StandardInput;
    myStreamWriter.Write(frame.Stream.ToArray());

    myProcess.OutputDataReceived += (sender, args) => Console.WriteLine($"FFPROBE: {args.Data}");
                
    myStreamWriter.Close();

    myProcess.WaitForExit();
}*/
}