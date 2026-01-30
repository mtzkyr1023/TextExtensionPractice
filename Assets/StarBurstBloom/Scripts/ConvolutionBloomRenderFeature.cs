using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace StarBurstBloom
{
    public class ConvolutionBloomRenderFeature : ScriptableRendererFeature
    {

        private ComputeShader fftxShader;
        private ComputeShader fftyShader;
        private ComputeShader ifftxShader;
        private ComputeShader ifftyShader;
        private ComputeShader multiplyShader;
        private ComputeShader spectralShader;
        private ComputeShader genPhaseShader;

        private Shader colorClipShader;
        private Shader bloomShader;
        private Shader polygonShader;

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

            if (genPhaseShader == null)
            {
                genPhaseShader = Resources.Load("GenPhase") as ComputeShader;
            }

            if (colorClipShader == null)
            {
                colorClipShader = Resources.Load("HDRClip") as Shader;
            }
            if (bloomShader == null)
            {
                bloomShader = Resources.Load("ConvolutionBloom") as Shader;
            }
            if (polygonShader == null)
            {
                polygonShader = Resources.Load("Polygon") as Shader;
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
                genPhaseShader);

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
}
