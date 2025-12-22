using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class ConvolutionBloomRenderPass : ScriptableRenderPass
{
#if UNITY_6000_0_OR_NEWER

    public class PSFPassData
    {
        public int vertexCount;
        public float rotation;
        public float lensSize;

        public Material material;
        public TextureHandle source;
    }

    public class ThresholdPassData
    {
        public float threshold;
        public Material material;
        public TextureHandle source;
    }

    public class BloomPassData
    {
        public float intensity;
        public Material material;
        public TextureHandle source;
        public TextureHandle bloom;
    }


    public class FFTPassData
    {
        public ComputeShader shader;
        public int kernelIndex;
        public int kernelSize;
        public TextureHandle srcRealTex;
        public TextureHandle srcImagTex;
        public TextureHandle dstRealTex;
        public TextureHandle dstImagTex;
    }

    public class MultiplyPassData
    {
        public ComputeShader shader;
        public int kernelIndex;
        public int kernelSize;

        public TextureHandle srcRealTex;
        public TextureHandle srcImagTex;
        public TextureHandle kernelRealTex;
        public TextureHandle kernelImagTex;
        public TextureHandle dstRealTex;
        public TextureHandle dstImagTex;
    }

    public class GenPhasePassData
    {
        public ComputeShader shader;
        public int kernelSize;

        public TextureHandle source;
        public TextureHandle dstReal;
        public TextureHandle dstImag;
    }


#else
    private RTHandle realTex0;
    private RTHandle imagTex0;
    private RTHandle realTex1;
    private RTHandle imagTex1;
    private RTHandle tempRenderTargetIdentifier;
    private RTHandle sourceRenderTargetIdentifier;
    private RTHandle kernelRealTex0;
    private RTHandle kernelImagTex0;
    private RTHandle kernelRealTex1;
    private RTHandle kernelImagTex1;
    private RTHandle kernelTex;
#endif


    private int kernelFFTY;
    private int kernelFFTX;
    private int kernelIFFTY;
    private int kernelIFFTX;
    private int kernelMultiply;
    private int kernelSpectrum;

    private int kernelSize = 1024;

    private Material colorClipMaterial;
    private Material bloomMaterial;
    private Material polygonMaterial;

    private ComputeShader fftxShader;
    private ComputeShader fftyShader;
    private ComputeShader ifftxShader;
    private ComputeShader ifftyShader;
    private ComputeShader convolutionShader;
    private ComputeShader spectralScaleShader;
    private ComputeShader genPhaseShader;

    private Shader colorClipShader;
    private Shader compositionShader;
    private Shader polygonShader;

    private int vertexCount;
    private float bloomThreshold;
    private float bloomIntensity;
    private float polygonRotation;
    private float lensSize;

    private static readonly int TempColorBufferId = UnityEngine.Shader.PropertyToID("_TempColorBuffer");
    private static readonly int SourceColorBufferId = UnityEngine.Shader.PropertyToID("_SourceColorBuffer");

    private static readonly int RealTex0BufferId = UnityEngine.Shader.PropertyToID("_RealTex0");
    private static readonly int RealTex1BufferId = UnityEngine.Shader.PropertyToID("_RealTex1");
    private static readonly int ImagTex0BufferId = UnityEngine.Shader.PropertyToID("_ImagTex0");
    private static readonly int ImagTex1BufferId = UnityEngine.Shader.PropertyToID("_ImagTex1");

    private static readonly int KernelRealTex0BufferId = UnityEngine.Shader.PropertyToID("_KernelRealTex0");
    private static readonly int KernelRealTex1BufferId = UnityEngine.Shader.PropertyToID("_KernelRealTex1");
    private static readonly int KernelImagTex0BufferId = UnityEngine.Shader.PropertyToID("_KernelImagTex0");
    private static readonly int KernelImagTex1BufferId = UnityEngine.Shader.PropertyToID("_KernelImagTex1");
    private static readonly int KernelBufferId = UnityEngine.Shader.PropertyToID("_KernelTex");


    public ConvolutionBloomRenderPass(
        Shader colorClipShader,
        Shader compositionShader,
        Shader polygonShader,
        ComputeShader fftx,
        ComputeShader ffty,
        ComputeShader ifftx,
        ComputeShader iffty,
        ComputeShader convolution,
        ComputeShader spectralScaleShader,
        ComputeShader genPhaseShader,
        int vertexCount,
        float polygonRotation,
        float bloomThreshold,
        float bloomIntensity,
        float lensSize)
    {
        this.colorClipShader = colorClipShader;
        this.compositionShader = compositionShader;
        this.polygonShader = polygonShader;
        this.colorClipMaterial = new Material(colorClipShader);
        this.bloomMaterial = new Material(compositionShader);
        if (polygonShader != null) this.polygonMaterial = new Material(polygonShader);
        this.fftxShader = fftx;
        this.fftyShader = ffty;
        this.ifftxShader = ifftx;
        this.ifftyShader = iffty;
        this.convolutionShader = convolution;
        this.spectralScaleShader = spectralScaleShader;
        this.genPhaseShader = genPhaseShader;
        this.vertexCount = vertexCount;
        this.polygonRotation = polygonRotation;
        this.bloomThreshold = bloomThreshold;
        this.bloomIntensity = bloomIntensity;
        this.lensSize = lensSize;

        this.kernelFFTX = fftxShader.FindKernel("FFTX");
        this.kernelFFTY = fftyShader.FindKernel("FFTY");
        this.kernelIFFTX = ifftxShader.FindKernel("IFFTX");
        this.kernelIFFTY = ifftyShader.FindKernel("IFFTY");
        if (spectralScaleShader)
        {
            this.kernelSpectrum = spectralScaleShader.FindKernel("CSMain");
        }
        this.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public void Setup()
    {
#if UNITY_6000_0_OR_NEWER
#else
        var desc0 = new RenderTextureDescriptor(kernelSize, kernelSize);
        desc0.colorFormat = RenderTextureFormat.ARGBFloat;
        desc0.enableRandomWrite = true;

        var desc1 = new RenderTextureDescriptor(kernelSize, kernelSize * 2);
        desc1.colorFormat = RenderTextureFormat.ARGBFloat;
        desc1.enableRandomWrite = true;

        tempRenderTargetIdentifier = RTHandles.Alloc(desc0, name:"_TempRenderTarget");
        sourceRenderTargetIdentifier = RTHandles.Alloc(desc0, name: "_SourceRenderTarget");
        kernelTex = RTHandles.Alloc(desc0, name: "_KernelTex");

        realTex0 = RTHandles.Alloc(desc1, name: "_RealTex0");
        realTex1 = RTHandles.Alloc(desc1, name: "_RealTex1");
        imagTex0 = RTHandles.Alloc(desc1, name: "_ImagTex0");
        imagTex1 = RTHandles.Alloc(desc1, name: "_ImagTex1");

        kernelRealTex0 = RTHandles.Alloc(desc1, name: "_KernelRealTex0");
        kernelRealTex1 = RTHandles.Alloc(desc1, name: "_KernelRealTex1");
        kernelImagTex0 = RTHandles.Alloc(desc1, name: "_KernelImagTex0");
        kernelImagTex1 = RTHandles.Alloc(desc1, name: "_KernelImagTex1");
#endif
    }


    public void Dispose()
    {
#if UNITY_6000_0_OR_NEWER
#else
        tempRenderTargetIdentifier?.Release();
        tempRenderTargetIdentifier = null;
        sourceRenderTargetIdentifier?.Release();
        sourceRenderTargetIdentifier = null;
        kernelTex?.Release();
        kernelTex = null;
        realTex0?.Release();
        realTex0 = null;
        realTex1?.Release();
        realTex1 = null;
        imagTex0?.Release();
        imagTex0 = null;
        imagTex1?.Release();
        imagTex1 = null;
        kernelRealTex0?.Release();
        kernelRealTex0 = null;
        kernelRealTex1?.Release();
        kernelRealTex1 = null;
        kernelImagTex0?.Release();
        kernelImagTex0 = null;
        kernelImagTex1?.Release();
        kernelImagTex1 = null;
#endif
    }


#if UNITY_6000_0_OR_NEWER
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        //[重要]カメラ関係のデータを取得する。
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        //[重要]リソース関係のデータ（カメラのテクスチャなど）を取得する。
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        //レンダリング関係のデータを取得する。
        UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
        //ライト関係のデータを取得する。
        UniversalLightData lightData = frameData.Get<UniversalLightData>();
        //シャドウ関係のデータを取得する。
        UniversalShadowData shadowData = frameData.Get<UniversalShadowData>();
        //ポストプロセス関係のデータを取得する。
        UniversalPostProcessingData postProcessData = frameData.Get<UniversalPostProcessingData>();

        TextureHandle source = resourceData.activeColorTexture;

        TextureDesc desc = source.GetDescriptor(renderGraph);

        desc.name = "CopyRenderTarget";
        TextureHandle sourceRenderTargetHandle = renderGraph.CreateTexture(desc);

        TextureDesc desc0 = new TextureDesc(kernelSize, kernelSize);
        desc0.format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;
        desc0.name = "TempRenderTarget";
        TextureHandle tempRenderTarget = renderGraph.CreateTexture(desc0);

        desc0.name = "KernelTex";
        TextureHandle kernelTex = renderGraph.CreateTexture(desc0);

        TextureDesc desc1 = new TextureDesc(kernelSize, kernelSize * 2);
        desc1.format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;
        desc1.enableRandomWrite = true;
        desc1.name = "KernelRealTex0";
        TextureHandle kernelRealTex0 = renderGraph.CreateTexture(desc1);

        desc1.name = "KernelImagTex0";
        TextureHandle kernelImagTex0 = renderGraph.CreateTexture(desc1);

        desc1.name = "KernelRealTex1";
        TextureHandle kernelRealTex1 = renderGraph.CreateTexture(desc1);

        desc1.name = "KernelImagTex1";
        TextureHandle kernelImagTex1 = renderGraph.CreateTexture(desc1);

        desc1.name = "RealTex0";
        TextureHandle realTex0 = renderGraph.CreateTexture(desc1);

        desc1.name = "ImagTex0";
        TextureHandle imagTex0 = renderGraph.CreateTexture(desc1);

        desc1.name = "RealTex1";
        TextureHandle realTex1 = renderGraph.CreateTexture(desc1);

        desc1.name = "ImagTex1";
        TextureHandle imagTex1 = renderGraph.CreateTexture(desc1);

        if (colorClipMaterial == null)
        {
            colorClipMaterial = new Material(colorClipShader);
        }
        if (polygonMaterial == null)
        {
            polygonMaterial = new Material(polygonShader);
        }

        using (var builder = renderGraph.AddRasterRenderPass<ThresholdPassData>("Convolution Bloom Threshold Pass", out ThresholdPassData passData))
        {
            passData.threshold = bloomThreshold;
            passData.material = colorClipMaterial;
            passData.source = source;

            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(tempRenderTarget, 0, AccessFlags.Write);

            builder.SetRenderFunc<ThresholdPassData>(static (passData, context) =>
            {
                passData.material.SetFloat("_Threshold", passData.threshold);
                Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, 0);
            });
        }


        using (var builder = renderGraph.AddRasterRenderPass<ThresholdPassData>("Convolution Bloom Source Copy Pass", out ThresholdPassData passData))
        {
            passData.threshold = bloomThreshold;
            passData.material = colorClipMaterial;
            passData.source = source;

            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(sourceRenderTargetHandle, 0, AccessFlags.Write);

            builder.SetRenderFunc<ThresholdPassData>(static (passData, context) =>
            {
                Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, 0, false);
            });
        }

        using (var builder = renderGraph.AddRasterRenderPass("Render PSF Base Pass", out PSFPassData passData))
        {
            passData.vertexCount = vertexCount;
            passData.rotation = polygonRotation;
            passData.lensSize = lensSize;
            passData.material = polygonMaterial;
            passData.source = source;

            builder.SetRenderAttachment(kernelTex, 0, AccessFlags.Write);

            builder.SetRenderFunc<PSFPassData>(static (passData, context) =>
            {
                passData.material.SetInt("_NCount", passData.vertexCount);
                passData.material.SetFloat("_Theta", passData.rotation * Mathf.Deg2Rad);
                passData.material.SetFloat("_Open", 0.0f);
                passData.material.SetFloat("_Size", passData.lensSize);
                Blitter.BlitTexture(context.cmd, Vector2.one, passData.material, 0);
            });
        }

        using (var builder = renderGraph.AddComputePass("PSF Phase Gen Pass", out GenPhasePassData passData))
        {
            passData.shader = genPhaseShader;
            passData.kernelSize = kernelSize;
            passData.source = kernelTex;
            passData.dstReal = kernelRealTex0;
            passData.dstImag = kernelImagTex0;

            builder.UseTexture(kernelTex, AccessFlags.Read);
            builder.UseTexture(kernelRealTex0, AccessFlags.ReadWrite);
            builder.UseTexture(kernelImagTex0, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (GenPhasePassData passData, ComputeGraphContext context) =>
            {
                int kernel = passData.shader.FindKernel("CSMain");
                context.cmd.SetComputeTextureParam(passData.shader, kernel, "Source", passData.source);
                context.cmd.SetComputeTextureParam(passData.shader, kernel, "DstReal", passData.dstReal);
                context.cmd.SetComputeTextureParam(passData.shader, kernel, "DstImag", passData.dstImag);
                context.cmd.SetComputeFloatParam(passData.shader, "Defocus", 0.0f);
                context.cmd.SetComputeFloatParam(passData.shader, "Aberration", 0.0f);
                context.cmd.SetComputeFloatParam(passData.shader, "PhaseNoiseStrength", 0.0f);

                context.cmd.DispatchCompute(passData.shader, kernel, passData.kernelSize / 8, passData.kernelSize / 8, 1);
            });
        }

        using (var builder = renderGraph.AddComputePass("PSF FFTX Pass", out FFTPassData passData))
        {
            passData.shader = fftxShader;
            passData.kernelIndex = kernelFFTX;
            passData.kernelSize = kernelSize;
            passData.srcRealTex = kernelRealTex0;
            passData.srcImagTex = kernelImagTex0;
            passData.dstRealTex = kernelRealTex1;
            passData.dstImagTex = kernelImagTex1;

            builder.UseTexture(passData.srcRealTex, AccessFlags.Read);
            builder.UseTexture(passData.srcImagTex, AccessFlags.Read);
            builder.UseTexture(passData.dstRealTex, AccessFlags.ReadWrite);
            builder.UseTexture(passData.dstImagTex, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (FFTPassData passData, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexReal", passData.srcRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexReal", passData.dstRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexImag", passData.dstImagTex);

                context.cmd.DispatchCompute(passData.shader, passData.kernelIndex, passData.kernelSize, 1, 1);
            });
        }

        using (var builder = renderGraph.AddComputePass("PSF FFTY Pass", out FFTPassData passData))
        {
            passData.shader = fftyShader;
            passData.kernelIndex = kernelFFTY;
            passData.kernelSize = kernelSize;
            passData.srcRealTex = kernelRealTex1;
            passData.srcImagTex = kernelImagTex1;
            passData.dstRealTex = kernelRealTex0;
            passData.dstImagTex = kernelImagTex0;

            builder.UseTexture(passData.srcRealTex, AccessFlags.Read);
            builder.UseTexture(passData.srcImagTex, AccessFlags.Read);
            builder.UseTexture(passData.dstRealTex, AccessFlags.ReadWrite);
            builder.UseTexture(passData.dstImagTex, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (FFTPassData passData, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexReal", passData.srcRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexImag", passData.srcImagTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexReal", passData.dstRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexImag", passData.dstImagTex);

                context.cmd.DispatchCompute(passData.shader, passData.kernelIndex, passData.kernelSize, 1, 1);
            });
        }

        using (var builder = renderGraph.AddComputePass("Spectrum Scale Pass", out FFTPassData passData))
        {
            passData.shader = spectralScaleShader;
            passData.kernelIndex = kernelSpectrum;
            passData.kernelSize = kernelSize;
            passData.srcRealTex = kernelRealTex0;
            passData.srcImagTex = kernelImagTex0;


            builder.UseTexture(passData.srcRealTex, AccessFlags.ReadWrite);
            builder.UseTexture(passData.srcImagTex, AccessFlags.Read);

            builder.SetRenderFunc(static (FFTPassData passData, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "Real", passData.srcRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "Imag", passData.srcImagTex);

                context.cmd.DispatchCompute(passData.shader, passData.kernelIndex, passData.kernelSize / 8, passData.kernelSize / 8, 1);
            });
        }

        using (var builder = renderGraph.AddComputePass("Kernel FFTX Pass", out FFTPassData passData))
        {
            passData.shader = fftxShader;
            passData.kernelIndex = kernelFFTX;
            passData.kernelSize = kernelSize;
            passData.srcRealTex = kernelRealTex0;
            passData.srcImagTex = kernelImagTex0;
            passData.dstRealTex = kernelRealTex1;
            passData.dstImagTex = kernelImagTex1;

            builder.UseTexture(passData.srcRealTex, AccessFlags.Read);
            builder.UseTexture(passData.dstRealTex, AccessFlags.ReadWrite);
            builder.UseTexture(passData.dstImagTex, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (FFTPassData passData, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexReal", passData.srcRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexReal", passData.dstRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexImag", passData.dstImagTex);

                context.cmd.DispatchCompute(passData.shader, passData.kernelIndex, passData.kernelSize, 1, 1);
            });
        }

        using (var builder = renderGraph.AddComputePass("Kernel FFTY Pass", out FFTPassData passData))
        {
            passData.shader = fftyShader;
            passData.kernelIndex = kernelFFTY;
            passData.kernelSize = kernelSize;
            passData.srcRealTex = kernelRealTex1;
            passData.srcImagTex = kernelImagTex1;
            passData.dstRealTex = kernelRealTex0;
            passData.dstImagTex = kernelImagTex0;

            builder.UseTexture(passData.srcRealTex, AccessFlags.Read);
            builder.UseTexture(passData.srcImagTex, AccessFlags.Read);
            builder.UseTexture(passData.dstRealTex, AccessFlags.ReadWrite);
            builder.UseTexture(passData.dstImagTex, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (FFTPassData passData, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexReal", passData.srcRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexImag", passData.srcImagTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexReal", passData.dstRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexImag", passData.dstImagTex);

                context.cmd.DispatchCompute(passData.shader, passData.kernelIndex, passData.kernelSize, 1, 1);
            });
        }

        using (var builder = renderGraph.AddComputePass("Scene FFTX Pass", out FFTPassData passData))
        {
            passData.shader = fftxShader;
            passData.kernelIndex = kernelFFTX;
            passData.kernelSize = kernelSize;
            passData.srcRealTex = tempRenderTarget;
            passData.dstRealTex = realTex0;
            passData.dstImagTex = imagTex0;


            builder.UseTexture(passData.srcRealTex, AccessFlags.Read);
            builder.UseTexture(passData.dstRealTex, AccessFlags.ReadWrite);
            builder.UseTexture(passData.dstImagTex, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (FFTPassData passData, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexReal", passData.srcRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexReal", passData.dstRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexImag", passData.dstImagTex);

                context.cmd.DispatchCompute(passData.shader, passData.kernelIndex, passData.kernelSize, 1, 1);
            });
        }

        using (var builder = renderGraph.AddComputePass("Scene FFTY Pass", out FFTPassData passData))
        {
            passData.shader = fftyShader;
            passData.kernelIndex = kernelFFTY;
            passData.kernelSize = kernelSize;
            passData.srcRealTex = realTex0;
            passData.srcImagTex = imagTex0;
            passData.dstRealTex = realTex1;
            passData.dstImagTex = imagTex1;

            builder.UseTexture(passData.srcRealTex, AccessFlags.Read);
            builder.UseTexture(passData.srcImagTex, AccessFlags.Read);
            builder.UseTexture(passData.dstRealTex, AccessFlags.ReadWrite);
            builder.UseTexture(passData.dstImagTex, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (FFTPassData passData, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexReal", passData.srcRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexImag", passData.srcImagTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexReal", passData.dstRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexImag", passData.dstImagTex);

                context.cmd.DispatchCompute(passData.shader, passData.kernelIndex, passData.kernelSize, 1, 1);
            });
        }

        using (var builder = renderGraph.AddComputePass("Spectrum Multiply Pass", out MultiplyPassData passData))
        {
            passData.shader = convolutionShader;
            passData.kernelIndex = kernelMultiply;
            passData.kernelSize = kernelSize;
            passData.srcRealTex = realTex1;
            passData.srcImagTex = imagTex1;
            passData.kernelRealTex = kernelRealTex0;
            passData.kernelImagTex = kernelImagTex0;
            passData.dstRealTex = realTex0;
            passData.dstImagTex = imagTex0;


            builder.UseTexture(passData.srcRealTex, AccessFlags.Read);
            builder.UseTexture(passData.srcImagTex, AccessFlags.Read);
            builder.UseTexture(passData.kernelRealTex, AccessFlags.Read);
            builder.UseTexture(passData.kernelImagTex, AccessFlags.Read);
            builder.UseTexture(passData.dstRealTex, AccessFlags.ReadWrite);
            builder.UseTexture(passData.dstImagTex, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (MultiplyPassData passData, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "KernelTexReal", passData.kernelRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "KernelTexImag", passData.kernelImagTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SceneTexReal", passData.srcRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SceneTexImag", passData.srcImagTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexReal", passData.dstRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexImag", passData.dstImagTex);

                context.cmd.DispatchCompute(passData.shader, passData.kernelIndex, passData.kernelSize / 8, passData.kernelSize / 8, 1);
            });
        }

        using (var builder = renderGraph.AddComputePass("Scene IFFTY Pass", out FFTPassData passData))
        {
            passData.shader = ifftyShader;
            passData.kernelIndex = kernelIFFTY;
            passData.kernelSize = kernelSize;
            passData.srcRealTex = realTex0;
            passData.srcImagTex = imagTex0;
            passData.dstRealTex = realTex1;
            passData.dstImagTex = imagTex1;

            builder.UseTexture(passData.srcRealTex, AccessFlags.Read);
            builder.UseTexture(passData.srcImagTex, AccessFlags.Read);
            builder.UseTexture(passData.dstRealTex, AccessFlags.ReadWrite);
            builder.UseTexture(passData.dstImagTex, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (FFTPassData passData, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexReal", passData.srcRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexImag", passData.srcImagTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexReal", passData.dstRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexImag", passData.dstImagTex);

                context.cmd.DispatchCompute(passData.shader, passData.kernelIndex, passData.kernelSize, 1, 1);
            });
        }

        using (var builder = renderGraph.AddComputePass("Scene IFFTX Pass", out FFTPassData passData))
        {
            passData.shader = ifftxShader;
            passData.kernelIndex = kernelIFFTX;
            passData.kernelSize = kernelSize;
            passData.srcRealTex = realTex1;
            passData.srcImagTex = imagTex1;
            passData.dstRealTex = realTex0;
            passData.dstImagTex = imagTex0;

            builder.UseTexture(passData.srcRealTex, AccessFlags.Read);
            builder.UseTexture(passData.srcImagTex, AccessFlags.Read);
            builder.UseTexture(passData.dstRealTex, AccessFlags.ReadWrite);
            builder.UseTexture(passData.dstImagTex, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (FFTPassData passData, ComputeGraphContext context) =>
            {
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexReal", passData.srcRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "SrcTexImag", passData.srcImagTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexReal", passData.dstRealTex);
                context.cmd.SetComputeTextureParam(passData.shader, passData.kernelIndex, "DstTexImag", passData.dstImagTex);

                context.cmd.DispatchCompute(passData.shader, passData.kernelIndex, passData.kernelSize, 1, 1);
            });
        }

        if (bloomMaterial)
        {
            using (var builder = renderGraph.AddRasterRenderPass<BloomPassData>("Convolution Bloom Combine Pass", out BloomPassData passData))
            {
                passData.intensity = bloomIntensity;
                passData.material = bloomMaterial;
                passData.bloom = realTex0;
                passData.source = sourceRenderTargetHandle;

                builder.UseTexture(sourceRenderTargetHandle, AccessFlags.Read);
                builder.UseTexture(realTex0, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);

                builder.SetRenderFunc<BloomPassData>(static (passData, context) =>
                {

                    passData.material.SetFloat("_Intensity", passData.intensity);
                    passData.material.SetTexture("_BloomTex", passData.bloom);
                    Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, 0);
                });
            }
        }
        else
        {
            bloomMaterial = new Material(compositionShader);
        }
    }
#else
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cb = CommandBufferPool.Get();

        var cameraColorRT = renderingData.cameraData.renderer.cameraColorTargetHandle;
        if (cameraColorRT == null || cameraColorRT.rt == null)
        {
            return;
        }

        if (colorClipMaterial)
        {
            colorClipMaterial.SetFloat("_Threshold", bloomThreshold);
            cb.Blit(cameraColorRT, tempRenderTargetIdentifier, colorClipMaterial);
        }
        else
        {
            colorClipMaterial = new Material(colorClipShader);
        }

        cb.Blit(cameraColorRT, sourceRenderTargetIdentifier);


        if (polygonMaterial != null)
        {
            polygonMaterial.SetInt("_NCount", vertexCount);
            polygonMaterial.SetFloat("_Size", polygonSize);
            polygonMaterial.SetFloat("_Theta", polygonRotation);
            polygonMaterial.SetFloat("_Open", lensOpen);

            cb.Blit(null, kernelTex, polygonMaterial);
        }

        cb.SetComputeTextureParam(fftxShader, kernelFFTX, "SrcTexReal", kernelTex);
        cb.SetComputeTextureParam(fftxShader, kernelFFTX, "DstTexReal", kernelRealTex0);
        cb.SetComputeTextureParam(fftxShader, kernelFFTX, "DstTexImag", kernelImagTex0);
        cb.DispatchCompute(fftxShader, kernelFFTX, kernelSize, 1, 1);

        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "SrcTexReal", kernelRealTex0);
        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "SrcTexImag", kernelImagTex0);

        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "DstTexReal", kernelRealTex1);
        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "DstTexImag", kernelImagTex1);
        cb.DispatchCompute(fftyShader, kernelFFTY, kernelSize, 1, 1);

        if (spectralScaleShader != null)
        {
            cb.SetComputeTextureParam(spectralScaleShader, kernelSpectral, "Imag", kernelImagTex1);
            cb.SetComputeTextureParam(spectralScaleShader, kernelSpectral, "Real", kernelRealTex1);
            cb.DispatchCompute(spectralScaleShader, kernelSpectral, kernelSize / 8, kernelSize / 8, 1);
        }

        cb.SetComputeTextureParam(fftxShader, kernelFFTX, "SrcTexReal", kernelRealTex1);
        cb.SetComputeTextureParam(fftxShader, kernelFFTX, "DstTexReal", kernelRealTex0);
        cb.SetComputeTextureParam(fftxShader, kernelFFTX, "DstTexImag", kernelImagTex0);
        cb.DispatchCompute(fftxShader, kernelFFTX, kernelSize, 1, 1);

        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "SrcTexReal", kernelRealTex0);
        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "SrcTexImag", kernelImagTex0);

        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "DstTexReal", kernelRealTex1);
        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "DstTexImag", kernelImagTex1);
        cb.DispatchCompute(fftyShader, kernelFFTY, kernelSize, 1, 1);

        cb.SetComputeTextureParam(fftxShader, kernelFFTX, "SrcTexReal", tempRenderTargetIdentifier);
        cb.SetComputeTextureParam(fftxShader, kernelFFTX, "DstTexReal", realTex0);
        cb.SetComputeTextureParam(fftxShader, kernelFFTX, "DstTexImag", imagTex0);

        cb.DispatchCompute(fftxShader, kernelFFTX, kernelSize, 1, 1);

        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "SrcTexReal", realTex0);
        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "SrcTexImag", imagTex0);

        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "DstTexReal", realTex1);
        cb.SetComputeTextureParam(fftyShader, kernelFFTY, "DstTexImag", imagTex1);

        cb.DispatchCompute(fftyShader, kernelFFTY, kernelSize, 1, 1);

        cb.SetComputeTextureParam(convolutionShader, kernelMultiply, "SceneTexReal", realTex1);
        cb.SetComputeTextureParam(convolutionShader, kernelMultiply, "SceneTexImag", imagTex1);

        cb.SetComputeTextureParam(convolutionShader, kernelMultiply, "KernelTexReal", kernelRealTex1);
        cb.SetComputeTextureParam(convolutionShader, kernelMultiply, "KernelTexImag", kernelImagTex1);

        cb.SetComputeTextureParam(convolutionShader, kernelMultiply, "DstTexReal", realTex0);
        cb.SetComputeTextureParam(convolutionShader, kernelMultiply, "DstTexImag", imagTex0);

        cb.DispatchCompute(convolutionShader, kernelMultiply, kernelSize / 8, kernelSize / 8, 1);

        cb.SetComputeTextureParam(ifftyShader, kernelIFFTY, "SrcTexReal", realTex0);
        cb.SetComputeTextureParam(ifftyShader, kernelIFFTY, "SrcTexImag", imagTex0);

        cb.SetComputeTextureParam(ifftyShader, kernelIFFTY, "DstTexReal", realTex1);
        cb.SetComputeTextureParam(ifftyShader, kernelIFFTY, "DstTexImag", imagTex1);

        cb.DispatchCompute(ifftyShader, kernelIFFTY, kernelSize, 1, 1);

        cb.SetComputeTextureParam(ifftxShader, kernelIFFTX, "SrcTexReal", realTex1);
        cb.SetComputeTextureParam(ifftxShader, kernelIFFTX, "SrcTexImag", imagTex1);

        cb.SetComputeTextureParam(ifftxShader, kernelIFFTX, "DstTexReal", realTex0);
        cb.SetComputeTextureParam(ifftxShader, kernelIFFTX, "DstTexImag", imagTex0);

        cb.DispatchCompute(ifftxShader, kernelIFFTX, kernelSize, 1, 1);

        if (bloomMaterial)
        {
            bloomMaterial.SetTexture("_BloomTex", realTex0);
            bloomMaterial.SetFloat("_Intensity", bloomIntensity);
            cb.Blit(sourceRenderTargetIdentifier, cameraColorRT, bloomMaterial);
        }
        else
        {
            bloomMaterial = new Material(compositionShader);
        }

        context.ExecuteCommandBuffer(cb);

        CommandBufferPool.Release(cb);
    }
#endif
}
