using UnityEditor;

namespace Stella3D.Demo
{
    [CustomEditor(typeof(DemoDescription))]
    public class DemoDescriptionInspector : Editor
    {
        SerializedProperty m_DescriptionProp;
        
        public void OnEnable()
        {
            m_DescriptionProp = serializedObject.FindProperty("Description");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_DescriptionProp);
            serializedObject.ApplyModifiedProperties();
        }
    }
}