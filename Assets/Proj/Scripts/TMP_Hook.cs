using UnityEngine;

public class TMP_Hook : TMPro.TextMeshPro
{
    [SerializeField]
    private ComputeShader GenerateMeshShader;

    protected override void OnValidate()
    {
        base.OnValidate();
    }
}
