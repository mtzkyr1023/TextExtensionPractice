using UnityEditor;
using UnityEngine;

[System.Serializable]
public class FontAtlas : ScriptableObject
{
    [SerializeField]
    public Texture2D fontAtlasTexture;

    public static void CreateFontAtlas(Texture2D texture, string path)
    {

        var asset = CreateInstance<FontAtlas>();
        asset.fontAtlasTexture = texture;

        AssetDatabase.CreateAsset(asset, path);

        EditorUtility.SetDirty(asset);

        AssetDatabase.SaveAssets();

        AssetDatabase.Refresh();
    }
}
