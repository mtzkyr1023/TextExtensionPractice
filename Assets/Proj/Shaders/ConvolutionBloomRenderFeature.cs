using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class ConvolutionBloomRenderFeature : ScriptableRendererFeature
{

    private ComputeShader fftxShader;
    private ComputeShader fftyShader;
    private ComputeShader ifftxShader;
    private ComputeShader ifftyShader;
    private ComputeShader multiplyShader;
    private ComputeShader spectralShader;

    [SerializeField] private Shader colorClipShader;
    [SerializeField] private Shader bloomShader;
    [SerializeField] private Shader polygonShader;

    [SerializeField, Range(3, 10)] private int vertexCount;
    [SerializeField, Range(0.0f, 1.0f)] private float polygonSize;
    [SerializeField, Range(0.0f, 128.0f)] private float bloomThreshold;
    [SerializeField, Range(0.0f, 32.0f)] private float bloomIntensity;
    [SerializeField, Range(0.0f, 3.141592f)] private float polygonRotation;
    [SerializeField, Range(0.0f, 1.0f)] private float lensOpen;

    private ConvolutionBloomRenderPass pass;

    public override void Create()
    {
        if (fftxShader == null)
        {
            fftxShader = Resources.Load("FFTX") as ComputeShader;
        }
        if (fftyShader == null)
        {
            fftyShader = Resources.Load("FFTY") as ComputeShader;
        }

        if (ifftxShader == null)
        {
            ifftxShader = Resources.Load("IFFTX") as ComputeShader;
        }
        if (ifftyShader == null)
        {
            ifftyShader = Resources.Load("IFFTY") as ComputeShader;
        }


        if (multiplyShader == null)
        {
            multiplyShader = Resources.Load("Multiply") as ComputeShader;
        }
        if (spectralShader == null)
        {
            spectralShader = Resources.Load("SpectralScale") as ComputeShader;
        }

        pass?.Dispose();
        this.name = "Convolution Bloom";
        pass = new ConvolutionBloomRenderPass(
            colorClipShader,
            bloomShader,
            polygonShader,
            fftxShader,
            fftyShader,
            ifftxShader,
            ifftyShader,
            multiplyShader,
            spectralShader,
            vertexCount,
            polygonSize,
            polygonRotation,
            bloomThreshold,
            bloomIntensity,
            lensOpen);

        pass.Setup();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            pass.Dispose();
        }
    }
}
