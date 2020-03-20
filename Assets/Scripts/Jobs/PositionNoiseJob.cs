using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Stella3D.SharedArray.Demo
{
    [BurstCompile]
    public struct PositionNoiseJob : IJobFor, IJobParallelFor
    {
        public NativeArray<float4x4> Matrices;

        public float SinTime;
        public float NoiseScale;

        public PositionNoiseJob(NativeArray<float4x4> matrices, float sinTime, float noiseScale)
        {
            Matrices = matrices;
            SinTime = sinTime;
            NoiseScale = noiseScale;
        }

        public void Execute(int i)
        {
            var m = Matrices[i];
            var mc = m.c3;
            var n = new float4(noise.srdnoise(mc.xy, mc.z) * NoiseScale, mc.w);
            m.c3 = math.lerp(mc, n, SinTime);
            Matrices[i] = m;
        }
    }
}
