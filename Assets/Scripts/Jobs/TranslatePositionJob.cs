using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Stella3D.Demo
{
    [BurstCompile]
    public struct TranslatePositionJob : IJobFor, IJobParallelFor
    {
        public NativeArray<float4x4> Matrices;

        public readonly float4 CenterDifferenceFromLast;

        public TranslatePositionJob(NativeArray<float4x4> matrices, float3 centerDifferenceFromLast)
        {
            Matrices = matrices;
            CenterDifferenceFromLast = new float4(centerDifferenceFromLast, 0f);
        }
    
        public void Execute(int i)
        {
            Matrices[i] = Matrices[i].TranslatePosition(CenterDifferenceFromLast);
        }
    }
}
