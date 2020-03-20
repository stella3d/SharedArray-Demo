using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Stella3D.Demo
{
    [BurstCompile]
    public struct ScaleUpdateJob : IJobFor, IJobParallelFor
    {
        public NativeArray<float4x4> Matrices;

        public readonly float3 Scale;

        public ScaleUpdateJob(NativeArray<float4x4> matrices, float3 scale)
        {
            Matrices = matrices;
            Scale = scale;
        }
    
        public void Execute(int i)
        {
            Matrices[i] = Matrices[i].Scaled(Scale);
        }
    }
}
