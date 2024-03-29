﻿using FlaneerMediaLib;
using FlaneerMediaLib.Logging;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace GLDisplayApp;

public class GLEnv
{
    internal GL Gl;

    private static BufferObject<float> Vbo;
    private static BufferObject<uint> Ebo;
    private static VertexArrayObject<float, uint> Vao;
    private readonly IWindow window;
    
    //Create a texture object.
    private static Texture Texture;
    private static Shader Shader;
    
    private int imageIdx = 1;
    private const int imageCount = 120;
    private DateTime lastDisplay = DateTime.Now;
    
    private DateTime StartTime = DateTime.Now;
    private int framesDisplayed;

    private Logger logger;
    
    private int currentSecond = DateTime.Now.Second;
    private int currentSecondFramesDisplayed;

    // OpenGL has image origin in the bottom-left corner.
    private static readonly float[] ScreenSpaceQuadVertices =
    {
        //X    Y      Z     U   V
        1.0f,  1.0f, 0.0f, 1f, 0f,
        1.0f, -1.0f, 0.0f, 1f, 1f,
        -1.0f, -1.0f, 0.0f, 0f, 1f,
        -1.0f,  1.0f, 1.0f, 0f, 0f
    };

    private static readonly uint[] ScreenSpaceQuadIndices =
    {
        0, 1, 3,
        1, 2, 3
    };

    private readonly UDPImageSource imageSource;
    private byte[] pixels;

    public GLEnv(GLWindow windowIn)
    {
        logger = Logger.GetLogger(this);
        
        
        ServiceRegistry.TryGetService<CommandLineArgumentStore>(out var clArgStore);
        var frameSettings = clArgStore.GetParams(CommandLineArgs.FrameSettings);
        var width = Int32.Parse(frameSettings[0]);
        var height = Int32.Parse(frameSettings[1]);

        pixels = new byte[width * height * 32];
        
        window = windowIn.window;

        imageSource = new UDPImageSource();

        window.Load += OnLoad;
        window.Render += OnRender;
        window.Update += OnUpdate;
        window.Closing += OnClose;
    }
    
    private void OnLoad()
    {
        //Getting the opengl api for drawing to the screen.
        Gl = GL.GetApi(window);

        Ebo = new BufferObject<uint>(Gl, ScreenSpaceQuadIndices, BufferTargetARB.ElementArrayBuffer);
        Vbo = new BufferObject<float>(Gl, ScreenSpaceQuadVertices, BufferTargetARB.ArrayBuffer);
        Vao = new VertexArrayObject<float, uint>(Gl, Vbo, Ebo);

        Vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5, 0);
        Vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5, 3);

        Shader = new Shader(this, "shader.vert", "shader.frag");

        //Loading a texture.
        Texture = new Texture(this, "testImage.png");
        StartTime = DateTime.Now;
    }
    
    private void OnUpdate(double obj)
    {
        unsafe
        {
            try
            {
                var frame = imageSource.GetImage();
                
                if (frame.Height != 0)
                {
                    Gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)frame.Width, (uint)frame.Height, 0,
                    PixelFormat.Rgb, PixelType.UnsignedByte, frame.FrameData);
                                    
                    if (currentSecond == DateTime.Now.Second)
                    {
                        currentSecondFramesDisplayed++;
                    }
                    else
                    {
                        logger.AmountStat("FPS", currentSecondFramesDisplayed);
                        currentSecond = DateTime.Now.Second;
                        currentSecondFramesDisplayed = 0;
                    }
                }
            }
            finally
            {
                window.Title = "Flaneer Streaming: " + StatLogging.GetPerfStats();
            }
            
        }

        
    }
    
    private unsafe void OnRender(double obj) //Method needs to be unsafe due to draw elements.
    {
        //Clear the color channel.
        Gl.Clear((uint) ClearBufferMask.ColorBufferBit);
        
        if((DateTime.Now - lastDisplay).Milliseconds > 33)
        {
            Texture.SetTextureFromImage($"TestImageSequence/out{imageIdx}.png");
            lastDisplay = DateTime.Now;
            imageIdx++;
            if (imageIdx > imageCount)
                imageIdx = 1;
        }

        Vao.Bind();
        Shader.Use();
        //Bind a texture and and set the uTexture0 to use texture0.
        Texture.Bind(TextureUnit.Texture0);
        Shader.SetUniform("uTexture0", 0);

        //Draw the geometry.
        Gl.DrawElements(PrimitiveType.Triangles, (uint) ScreenSpaceQuadIndices.Length, DrawElementsType.UnsignedInt, null);
        
        
        framesDisplayed++;
        var averageFrameTime = (DateTime.Now - StartTime) / framesDisplayed;
        StatLogging.LogPerfStat("GL-FPS", 1000/averageFrameTime.Milliseconds);
    }
    
    private void OnClose()
    {
        Vbo.Dispose();
        Ebo.Dispose();
        Vao.Dispose();
        Shader.Dispose();
        //Remember to dispose the texture.
        Texture.Dispose();
    }
}
