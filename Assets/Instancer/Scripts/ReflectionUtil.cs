using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stella3D
{
    public delegate void DrawMeshInstancedInternalDelegate(Mesh mesh, int subMeshIndex, Material material,
        Matrix4x4[] matrices, int count, 
        MaterialPropertyBlock properties = null,
        ShadowCastingMode castShadows = ShadowCastingMode.On,
        bool receiveShadows = true,
        int layer = 0,
        Camera camera = null,
        LightProbeUsage lightProbeUsage = LightProbeUsage.BlendProbes,
        LightProbeProxyVolume lightProbeProxyVolume = null);

    static class ReflectionUtil
    {
        public static DrawMeshInstancedInternalDelegate Get_DrawMeshInstanced_InternalMethod()
        {
            const BindingFlags bindFlags = BindingFlags.NonPublic | BindingFlags.Static;
            var methodInfo = typeof(Graphics).GetMethod("Internal_DrawMeshInstanced", bindFlags);

            return methodInfo == null ? null : (DrawMeshInstancedInternalDelegate) 
                methodInfo.CreateDelegate(typeof(DrawMeshInstancedInternalDelegate));
        }
    }
}