#include "CaptureRuntime.h"

#include "DDAImpl.h"
#include "Defs.h"
#include "nvEncodeAPI.h"
#include "NvEncoderD3D11.h"

CaptureRuntime::CaptureRuntime(const VideoCaptureSettings capture_settings, const H264CodecSettings codec_settings)
{
    Width = capture_settings.Width;
    Height = capture_settings.Height;
    MaxFPS = capture_settings.MaxFPS;

    Codec_Id = NV_ENC_CODEC_H264_GUID;
    BufferFormat = codec_settings.Format;
    GoPLength = codec_settings.GoPLength;
}

CaptureRuntime::~CaptureRuntime()
{
    Cleanup();
}

std::vector <IDXGIAdapter*> EnumerateAdapters(void)
{
    IDXGIAdapter* pAdapter;
    std::vector <IDXGIAdapter*> vAdapters;
    IDXGIFactory* pFactory = NULL;


    // Create a DXGIFactory object.
    if (FAILED(CreateDXGIFactory(__uuidof(IDXGIFactory), (void**)&pFactory)))
    {
        return vAdapters;
    }


    for (UINT i = 0;
        pFactory->EnumAdapters(i, &pAdapter) != DXGI_ERROR_NOT_FOUND;
        ++i)
    {
        vAdapters.push_back(pAdapter);
    }


    if (pFactory)
    {
        pFactory->Release();
    }

    return vAdapters;

}

/// Initialize DXGI pipeline
HRESULT CaptureRuntime::InitDXGI()
{
    HRESULT hr = S_OK;
    /// Driver types supported
    D3D_DRIVER_TYPE DriverTypes[] =
    {
        D3D_DRIVER_TYPE_HARDWARE,
        D3D_DRIVER_TYPE_WARP,
        D3D_DRIVER_TYPE_REFERENCE,
    };
    UINT NumDriverTypes = ARRAYSIZE(DriverTypes);

    /// Feature levels supported
    D3D_FEATURE_LEVEL FeatureLevels[] =
    {
        D3D_FEATURE_LEVEL_11_0,
        D3D_FEATURE_LEVEL_10_1,
        D3D_FEATURE_LEVEL_10_0,
        D3D_FEATURE_LEVEL_9_1
    };
    UINT NumFeatureLevels = ARRAYSIZE(FeatureLevels);
    D3D_FEATURE_LEVEL FeatureLevel = D3D_FEATURE_LEVEL_11_0;

    /// Create device
    for (UINT DriverTypeIndex = 0; DriverTypeIndex < NumDriverTypes; ++DriverTypeIndex)
    {
        auto adapters = EnumerateAdapters();

        for (const auto adapter : adapters)
        {
	        hr = D3D11CreateDevice(adapter, DriverTypes[DriverTypeIndex], nullptr,
            D3D11_CREATE_DEVICE_DEBUG, FeatureLevels, NumFeatureLevels,
            D3D11_SDK_VERSION, &D3DDevice, &FeatureLevel, &deviceContext);
        
	        if (SUCCEEDED(hr))
	        {
	            // Device creation succeeded, no need to loop anymore
                return hr;
	        }
        }     
    }
    return hr;
}

/// Initialize DDA handler
HRESULT CaptureRuntime::InitDup()
{
    HRESULT hr = S_OK;
    if (!ddaWrapper)
    {
        ddaWrapper = new DDAImpl(D3DDevice, deviceContext);
        hr = ddaWrapper->Init();
        returnIfError(hr);
    }
    return hr;
}

/// Initialize NVENCODEAPI wrapper
HRESULT CaptureRuntime::InitEnc()
{
    if (!encoder)
    {
        //std::cout << "Video details: Width:" << w << " Height:" << h << " Format:" << fmt << "\n";

        encoder = new NvEncoderD3D11(D3DDevice, Width, Height, BufferFormat);
        if (!encoder)
        {
            returnIfError(E_FAIL);
        }

        ZeroMemory(&encInitParams, sizeof(encInitParams));
        encInitParams.encodeConfig = &encConfig;
        encInitParams.encodeWidth = Width;
        encInitParams.encodeHeight = Height;
        encInitParams.maxEncodeWidth = UHD_W;
        encInitParams.maxEncodeHeight = UHD_H;
        encInitParams.frameRateNum = MaxFPS;
        encInitParams.frameRateDen = 1;

        ZeroMemory(&encConfig, sizeof(encConfig));
        encConfig.gopLength = GoPLength;

        try
        {
            encoder->CreateDefaultEncoderParams(&encInitParams, Codec_Id, NV_ENC_PRESET_LOW_LATENCY_HP_GUID);
            encoder->CreateEncoder(&encInitParams);
        }
        catch (...)
        {
            returnIfError(E_FAIL);
        }
    }
    return S_OK;
}

HRESULT CaptureRuntime::Init()
{
    HRESULT hr = S_OK;

    hr = InitDXGI();
    returnIfError(hr);

    hr = InitDup();
    returnIfError(hr);

    hr = InitEnc();
    returnIfError(hr);

    return hr;
}

/// Capture a frame using DDA
HRESULT CaptureRuntime::Capture()
{
    if (ddaWrapper)
        return ddaWrapper->GetCapturedFrame(&dupTex2D, timeout()); // Release after preproc
    else
        return 1;
}

/// Preprocess captured frame
HRESULT CaptureRuntime::Preproc()
{
    HRESULT hr = S_OK;
    const NvEncInputFrame* pEncInput = encoder->GetNextInputFrame();
    encBuf = (ID3D11Texture2D*)pEncInput->inputPtr;
    deviceContext->CopySubresourceRegion(encBuf, D3D11CalcSubresource(0, 0, 1), 0, 0, 0, dupTex2D, 0, NULL);
    SAFE_RELEASE(dupTex2D);
    returnIfError(hr);

    encBuf->AddRef();  // Release after encode
    return hr;
}

/// Encode the captured frame using NVENCODEAPI
HRESULT CaptureRuntime::Encode()
{
    HRESULT hr = S_OK;
    try
    {
        encoder->EncodeFrame(vPacket);
    }
    catch (...)
    {
        hr = E_FAIL;
    }
    SAFE_RELEASE(encBuf);
    return hr;
}

HRESULT CaptureRuntime::FulfilFrameRequest(FrameRequest& frame_request)
{
    auto hr = Capture();
    if (FAILED(hr))
    {
        return hr;
    }
    /// Preprocess for encoding
    hr = Preproc();
    if (FAILED(hr))
    {
        printf("Preproc failed with error 0x%08x\n", hr);
        return hr;
    }
    hr = Encode();
    if (FAILED(hr))
    {
        printf("Encode failed with error 0x%08x\n", hr);
        return hr;
    }
    frame_request.Data = vPacket.back().data();
    return hr;
}

void CaptureRuntime::Cleanup()
{
    if (ddaWrapper)
    {
        ddaWrapper->Cleanup();
        delete ddaWrapper;
        ddaWrapper = nullptr;
    }

    if (encoder)
    {
        ZeroMemory(&encInitParams, sizeof(NV_ENC_INITIALIZE_PARAMS));
        ZeroMemory(&encConfig, sizeof(NV_ENC_CONFIG));
    }

    SAFE_RELEASE(dupTex2D);
    if (encoder)
    {
        /// Flush the encoder and write all output to file before destroying the encoder
        /*encoder->EndEncode(vPacket);
        WriteEncOutput();
        encoder->DestroyEncoder();
        if (bDelete)
        {
            delete encoder;
            encoder = nullptr;
        }*/

        ZeroMemory(&encInitParams, sizeof(NV_ENC_INITIALIZE_PARAMS));
        ZeroMemory(&encConfig, sizeof(NV_ENC_CONFIG));
    }

    SAFE_RELEASE(D3DDevice);
    SAFE_RELEASE(deviceContext);
}