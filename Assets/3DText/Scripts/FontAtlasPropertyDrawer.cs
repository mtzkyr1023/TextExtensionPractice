using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FontAtlas))]
public class FontAtlasPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Begin the property scope to handle prefab overrides
        EditorGUI.BeginProperty(position, label, property);

        // Get the SerializedProperties for the fields within MyTextureHolder
        SerializedProperty myTextureProperty = property.FindPropertyRelative("fontAtlasTexture");

        // Calculate heights for drawing
        float singleLineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        // Draw the default Texture2D field
        Rect textureFieldRect = new Rect(position.x, position.y, position.width, 500);
        EditorGUI.ObjectField(textureFieldRect, myTextureProperty, typeof(Texture2D));

        // End the property scope
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Calculate the total height needed for both fields
        float singleLineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        return (singleLineHeight * 1) + spacing;
    }
}
