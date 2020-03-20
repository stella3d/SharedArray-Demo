using UnityEditor;

namespace Stella3D.SharedArray.Demo
{
    [CustomEditor(typeof(SharedArrayMeshDrawerDemo))]
    public class SharedArrayMeshDrawerInspector : Editor
    {
        const string k_Help = "Try changing the Transform above this,\nor the effect parameters below!";

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(k_Help, MessageType.Info);
            DrawDefaultInspector();
        }
    }
}