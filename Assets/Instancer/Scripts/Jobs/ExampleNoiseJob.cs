using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct ExampleNoiseJob : IJobParallelFor
{
    public NativeArray<Matrix4x4> Matrices;

    public float SinTime;
    
    public void Execute(int i)
    {
        var m = Matrices[i];
        var p = new float3((Vector3) m.GetColumn(3));
        var n = noise.psrdnoise(p.xy, p.yz) * 10f;
        var l = math.lerp(p, n, SinTime);
        Vector4 newP = new Vector4(l.x, l.y, l.z, 1f);
        m.SetColumn(3, newP);
        Matrices[i] = m;
    }
}
