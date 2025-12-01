using UnityEngine;

public class BoardGenerator : MonoBehaviour
{
    [SerializeField]
    private ComputeShader boardGenerateShader;

    private int boardGenerateKernel;

    private const int size = 512;

    private RenderTexture volumeTexture;
    private ComputeBuffer renderTextBuffer;

    private void Start()
    {
        boardGenerateKernel = boardGenerateShader.FindKernel("BoardGeneratorShaderMain");
        
        if (volumeTexture)
        {
            volumeTexture?.Release();
            volumeTexture = null;
        }

        var rd = new RenderTextureDescriptor(size, size);
        rd.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        rd.volumeDepth = size;
        rd.colorFormat = RenderTextureFormat.RFloat;
        rd.enableRandomWrite = true;

        volumeTexture = new RenderTexture(rd);
        volumeTexture.Create();
    }

    private void OnDestroy()
    {
        if (volumeTexture)
        {
            volumeTexture?.Release();
            volumeTexture = null;
        }
    }

    private void Update()
    {
        boardGenerateShader.Dispatch(boardGenerateKernel, size / 8, size / 8, size / 8);
    }
}
