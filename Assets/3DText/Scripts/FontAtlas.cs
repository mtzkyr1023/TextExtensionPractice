using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class FontAtlas : ScriptableObject
{
    [SerializeField]
    internal Texture2D fontAtlasTexture;

    [SerializeField]
    internal Dictionary<char, int> characterIndex;

    [SerializeField]
    internal int fontSize;

    [SerializeField]
    internal int atlasWidth;

    [SerializeField]
    internal int atlasHeight;

    public static FontAtlas CreateFontAtlas(Texture2D texture, Dictionary<char, int> index, int size, int width, int height)
    {
        var asset = CreateInstance<FontAtlas>();
        asset.fontAtlasTexture = texture;
        asset.characterIndex = index;
        asset.fontSize = size;
        asset.atlasWidth = width;
        asset.atlasHeight = height;

        return asset;
    }
}
