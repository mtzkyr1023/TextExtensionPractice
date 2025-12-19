using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ConvolutionBloomRenderPass : ScriptableRenderPass
{
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

    private int kernelFFTY;
    private int kernelFFTX;
    private int kernelIFFTY;
    private int kernelIFFTX;
    private int kernelMultiply;
    private int kernelSpectral;

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

    private Shader colorClipShader;
    private Shader compositionShader;
    private Shader polygonShader;

    private int vertexCount;
    private float polygonSize;
    private float bloomThreshold;
    private float bloomIntensity;
    private float polygonRotation;
    private float lensOpen;

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
        int vertexCount,
        float polygonSize,
        float polygonRotation,
        float bloomThreshold,
        float bloomIntensity,
        float lensOpen)
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
        this.vertexCount = vertexCount;
        this.polygonSize = polygonSize;
        this.polygonRotation = polygonRotation;
        this.bloomThreshold = bloomThreshold;
        this.bloomIntensity = bloomIntensity;
        this.lensOpen = lensOpen;

        this.kernelFFTX = fftxShader.FindKernel("FFTX");
        this.kernelFFTY = fftyShader.FindKernel("FFTY");
        this.kernelIFFTX = ifftxShader.FindKernel("IFFTX");
        this.kernelIFFTY = ifftyShader.FindKernel("IFFTY");
        if (spectralScaleShader)
        {
            this.kernelSpectral = spectralScaleShader.FindKernel("CSMain");
        }
        this.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        base.OnCameraSetup(cmd, ref renderingData);
    }

    public void Setup()
    {
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
    }


    public void Dispose()
    {
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
    }



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

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        base.OnCameraCleanup(cmd);
    }
}
