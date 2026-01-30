using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StarBurstBloom
{
    [VolumeComponentMenu("Post-processing Custom/Star Burst Bloom")]


#if UNITY_6000_0_OR_NEWER
    [VolumeRequiresRendererFeatures(typeof(ConvolutionBloomRenderFeature))]

    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
#endif

    public class ConvolutionBloom : VolumeComponent, IPostProcessComponent
    {
        public ClampedIntParameter bladeCount = new ClampedIntParameter(6, 3, 10);
        public ClampedFloatParameter threshold = new ClampedFloatParameter(10.0f, 0.1f, 128.0f);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0.0f, 0.0f, 32.0f);
        public ClampedFloatParameter rotation = new ClampedFloatParameter(0.0f, 0.0f, 360.0f);
        public ClampedFloatParameter size = new ClampedFloatParameter(0.7f, 0.3f, 0.8f);

        public ConvolutionBloom()
        {
            displayName = "Star Burst Bloom";
        }

        public bool IsActive()
        {
            return intensity.value > 0.0f;
        }

#if !UNITY_6000_0_OR_NEWER
        public bool IsTileCompatible()
        {
            return false;
        }
#endif
    }
}
