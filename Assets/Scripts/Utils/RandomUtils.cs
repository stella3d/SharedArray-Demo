using UnityEngine;
using Random = UnityEngine.Random;

static class RandomUtils
{
    public static Matrix4x4[] Matrices(Vector3 center, int count, float scale = 15f)
    {
        var matrices = new Matrix4x4[count];
        for (int i = 0; i < matrices.Length; i++)
        {
            var pos = center + Random.onUnitSphere * scale;
            var rotation = Quaternion.LookRotation(pos, Vector3.up);
            matrices[i] = Matrix4x4.TRS(pos, rotation, Vector3.one);
        }

        return matrices;
    }
    
    public static Vector4[] Colors(int count)
    {
        var colors = new Vector4[count];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0f, 1f, 0.2f, 0.8f);
        }
        
        return colors;
    }
}
