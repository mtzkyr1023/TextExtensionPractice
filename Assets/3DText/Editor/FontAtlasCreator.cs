using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;


public static class FontEngineProxy
{
    private static Func<Glyph, int, GlyphRenderMode, Texture2D, FontEngineError> renderGlyphToTextureDelegate;

    public static FontEngineError RenderGlyphToTexture(
        Glyph glyph,
        int padding,
        GlyphRenderMode renderMode,
        Texture2D texture)
    {
        if (renderGlyphToTextureDelegate == null)
        {
            var method = typeof(FontEngine).GetMethod("RenderGlyphToTexture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            renderGlyphToTextureDelegate =
                (Func<Glyph, int, GlyphRenderMode, Texture2D, FontEngineError>)Delegate.CreateDelegate(
                    typeof(Func<Glyph, int, GlyphRenderMode, Texture2D, FontEngineError>),
                    null, method);
        }

        return renderGlyphToTextureDelegate.Invoke(glyph, padding, renderMode, texture);
    }
}


public class FontAtlasCreatorWindow : EditorWindow
{

    private ComputeShader sobelFilterShader;
    private Font font;
    private TextAsset texts;
    private int fontSize;
    private int maxWidth;
    
    private List<Glyph> glyphs;
    private Texture2D baseAtlasTexture;
    private RenderTexture blurTexture;
    private RenderTexture insideTexture;
    private RenderTexture outsideTexture;
    private RenderTexture resultTexture;
    private RenderTexture sdfTexture;

    private Dictionary<char, int> characterIndex;

    private int blurPassKernel;
    private int firstPassFilterKernel;
    private int secondPassFilterKernel;
    private int thirdPassFilterKernel;

    private int width;
    private int height;

    [MenuItem("EasyAssets/FontAtlasCreator")]
    private static void ShowWindow()
    {
        FontAtlasCreatorWindow window = GetWindow<FontAtlasCreatorWindow>();
    }

    private void OnGUI()
    {
        sobelFilterShader = (ComputeShader)EditorGUILayout.ObjectField("SobelFilterShader", sobelFilterShader, typeof(ComputeShader), false);
        font = (Font)EditorGUILayout.ObjectField("BaseFontAsset", font, typeof(Font), false);
        texts = (TextAsset)EditorGUILayout.ObjectField("Texts", texts, typeof(TextAsset), false);
        fontSize = EditorGUILayout.IntField("FontSize", fontSize);
        maxWidth = EditorGUILayout.IntField("Width", maxWidth);

        if (GUILayout.Button("Create"))
        {
            CreateAtlas();

            GenerateSDFTexture();

            SaveSDFAtlas();

            Release();
        }
    }

    private void CreateAtlas()
    {

        if (!font)
        {
            Debug.LogError("FontAtlasCreator:font is null.");
            return;
        }

        if (!texts)
        {
            Debug.LogError("FontAtlasCreator:texts is null.");
            return;
        }

        FontEngine.InitializeFontEngine();
        FontEngine.LoadFontFace(font, fontSize);

        glyphs = new List<Glyph>();

        characterIndex = new Dictionary<char, int>();

        int index = 0;

        foreach (var character in texts.text)
        {
            if (FontEngine.TryGetGlyphWithUnicodeValue(character, GlyphLoadFlags.LOAD_COMPUTE_METRICS | GlyphLoadFlags.LOAD_NO_BITMAP, out var glyph))
            {
                glyphs.Add(glyph);
                characterIndex.Add(character, index);
                index++;
            }
        }

        if (glyphs.Count == 0)
        {
            Debug.LogError("FontAtlasCreator:glyphs.Count is empty.");
            return;
        }

        width = maxWidth;
        int columnCount = width / fontSize;
        int rowCount = maxWidth / fontSize;
        height = (glyphs.Count / rowCount / columnCount + 1) * maxWidth;

        baseAtlasTexture = new Texture2D(width, height, TextureFormat.R8, false);

        var clearColor = new Color[width * height];
        for (int i = 0; i < width * height; i++)
        {
            clearColor[i] = Color.clear;
        }

        baseAtlasTexture.SetPixels(clearColor);

        for (int i = 0; i < glyphs.Count; i++)
        {
            int x = i % columnCount * fontSize;
            int y = height - i / columnCount * fontSize - fontSize;

            Glyph glyph = glyphs[i];
            glyph.glyphRect = new GlyphRect(x, y, fontSize, fontSize);

            FontEngineProxy.RenderGlyphToTexture(glyph, 9, GlyphRenderMode.SMOOTH, baseAtlasTexture);
        }

        baseAtlasTexture.Apply();
    }

    private void GenerateSDFTexture()
    {
        {
            RenderTextureDescriptor desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.RFloat);
            desc.dimension = TextureDimension.Tex2D;
            desc.enableRandomWrite = true;
            blurTexture = new RenderTexture(desc);
        }

        {
            RenderTextureDescriptor desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBFloat);
            desc.dimension = TextureDimension.Tex2D;
            desc.enableRandomWrite = true;
            insideTexture = new RenderTexture(desc);
        }

        {
            RenderTextureDescriptor desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBFloat);
            desc.dimension = TextureDimension.Tex2D;
            desc.enableRandomWrite = true;
            outsideTexture = new RenderTexture(desc);
        }

        {
            RenderTextureDescriptor desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32);
            desc.dimension = TextureDimension.Tex2D;
            desc.enableRandomWrite = true;
            resultTexture = new RenderTexture(desc);
        }

        {
            RenderTextureDescriptor desc = new RenderTextureDescriptor(width / 1, height / 1, RenderTextureFormat.ARGB32);
            desc.dimension = TextureDimension.Tex2D;
            desc.enableRandomWrite = true;
            sdfTexture = new RenderTexture(desc);
        }

        blurPassKernel = sobelFilterShader.FindKernel("BlurPass");
        firstPassFilterKernel = sobelFilterShader.FindKernel("FirstPassFilter");
        secondPassFilterKernel = sobelFilterShader.FindKernel("SecondPassFilter");
        thirdPassFilterKernel = sobelFilterShader.FindKernel("ThirdPassFilter");

        sobelFilterShader.SetTexture(blurPassKernel, "SourceTex", baseAtlasTexture);
        sobelFilterShader.SetTexture(blurPassKernel, "ResultBlurTex", blurTexture);

        sobelFilterShader.SetTexture(firstPassFilterKernel, "SourceTex", blurTexture);
        sobelFilterShader.SetTexture(firstPassFilterKernel, "ResultInside", insideTexture);
        sobelFilterShader.SetTexture(firstPassFilterKernel, "ResultOutside", outsideTexture);

        sobelFilterShader.SetTexture(secondPassFilterKernel, "ResultInside", insideTexture);
        sobelFilterShader.SetTexture(secondPassFilterKernel, "ResultOutside", outsideTexture);


        sobelFilterShader.SetTexture(thirdPassFilterKernel, "ResultInside", insideTexture);
        sobelFilterShader.SetTexture(thirdPassFilterKernel, "ResultOutside", outsideTexture);
        sobelFilterShader.SetTexture(thirdPassFilterKernel, "Result", resultTexture);

        sobelFilterShader.SetFloat("maxInside", fontSize / 4);
        sobelFilterShader.SetFloat("maxOutside", fontSize / 4);

        sobelFilterShader.Dispatch(blurPassKernel, width / 1, height / 1, 1);

        sobelFilterShader.Dispatch(firstPassFilterKernel, width / 1, height / 1, 1);

        for (int i = 0; i < 64; i++)
        {
            sobelFilterShader.Dispatch(secondPassFilterKernel, width / 1, height / 1, 1);
        }

        sobelFilterShader.Dispatch(thirdPassFilterKernel, width / 1, height / 1, 1);

        Graphics.Blit(resultTexture, sdfTexture);
    }

    private void SaveSDFAtlas()
    {
        RenderTexture tmp = RenderTexture.active;
        var path = EditorUtility.SaveFilePanelInProject(title: "Save Texture", defaultName: "test", extension: "asset", message: "Save Texture");
        if (path == null)
        {
            RenderTexture.active = tmp;
            Debug.LogError("FontAtlasCreator:path is null.");
            return;
        }

        RenderTexture.active = sdfTexture;

        Texture2D texture = new Texture2D(width / 1, height / 1, TextureFormat.Alpha8, false);
        texture.ReadPixels(new Rect(0, 0, width / 1, height / 1), 0, 0);
        texture.Apply();

        RenderTexture.active = tmp;

        texture.name = "Font Atlas";
        FontAtlas atlas = FontAtlas.CreateFontAtlas(texture, characterIndex, fontSize, width, height);


        AssetDatabase.CreateAsset(atlas, path);

        AssetDatabase.AddObjectToAsset(texture, atlas);

        EditorUtility.SetDirty(atlas);

        AssetDatabase.SaveAssets();

        AssetDatabase.Refresh();
    }

    private void Release()
    {
        if (baseAtlasTexture)
        {
            DestroyImmediate(baseAtlasTexture);
            baseAtlasTexture = null;
        }
        insideTexture?.Release();
        insideTexture = null;

        outsideTexture?.Release();
        outsideTexture = null;

        resultTexture?.Release();
        resultTexture = null;

        sdfTexture?.Release();
        sdfTexture = null;
    }
}
