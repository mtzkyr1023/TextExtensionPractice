using UnityEngine;

public class TMP_Hook : TMPro.TextMeshPro
{
    [SerializeField]
    private ComputeShader GenerateMeshShader;
#if WITH_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
    }
#endif
}
