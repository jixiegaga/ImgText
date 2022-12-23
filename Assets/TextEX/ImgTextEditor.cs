using UnityEditor;
using UnityEditor.UI;

[CanEditMultipleObjects]
[CustomEditor(typeof(ImgText), true)]
public class ImgTextEditor : GraphicEditor
{
    SerializedProperty m_OriginalText;
    SerializedProperty m_FontData;
    SerializedProperty m_underLineWidth;

    protected override void OnEnable()
    {
        base.OnEnable();
        m_OriginalText = serializedObject.FindProperty("originalText");
        m_FontData = serializedObject.FindProperty("m_FontData");
        m_underLineWidth = serializedObject.FindProperty("underLineWidth");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(m_OriginalText);
        EditorGUILayout.PropertyField(m_FontData);
        EditorGUILayout.PropertyField(m_underLineWidth);

        AppearanceControlsGUI();
        RaycastControlsGUI();

        serializedObject.ApplyModifiedProperties();
    }
}
