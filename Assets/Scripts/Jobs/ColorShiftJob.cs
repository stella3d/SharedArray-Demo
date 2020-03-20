using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct ColorShiftJob : IJobFor, IJobParallelFor
{
    public NativeArray<float4> Colors;

    public float SinTime;

    public ColorShiftJob(NativeArray<float4> colors, float sinTime)
    {
        Colors = colors;
        SinTime = sinTime;
    }
    
    public void Execute(int i)
    {
        var c = Colors[i];
        var noiseColor = new float4(noise.srdnoise(new float2(c.x, c.y), c.z), c.w);
        Colors[i] = math.lerp(c, noiseColor, SinTime);
    }
}
