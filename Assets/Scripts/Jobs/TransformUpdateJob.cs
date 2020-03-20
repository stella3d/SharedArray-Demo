using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Stella3D.Demo
{
    [BurstCompile]
    public struct TransformUpdateJob : IJobFor, IJobParallelFor
    {
        public NativeArray<float4x4> Matrices;

        public readonly float3 Scale;
        public readonly float4 CenterDifferenceFromLast;

        public TransformUpdateJob(NativeArray<float4x4> matrices, float3 scale, float3 centerDifferenceFromLast)
        {
            Matrices = matrices;
            Scale = scale;
            CenterDifferenceFromLast = new float4(centerDifferenceFromLast, 0f);;
        }
    
        public void Execute(int i)
        {
            Matrices[i] = Matrices[i].TranslatePosition(CenterDifferenceFromLast).Scaled(Scale);
        }
    }
    
    // this job does not really set the rotation correctly but it's wrong in a way that looks cooler than being right
    [BurstCompile]
    public struct OriginRotationUpdateJob : IJobFor, IJobParallelFor
    {
        public NativeArray<float4x4> Matrices;

        public readonly float3 Scale;
        public readonly float3 Up;

        public readonly Quaternion OriginRotation;

        public OriginRotationUpdateJob(NativeArray<float4x4> matrices, float3 scale, quaternion rotation = default)
        {
            Matrices = matrices;
            Scale = scale;
            Up = Vector3.up;
            OriginRotation = rotation.Equals(default) ? quaternion.identity : rotation;
        }
    
        public void Execute(int i)
        {
            var m = Matrices[i];
            var p = m.c3;
            var position = new float3(p.x, p.y, p.z);
            var rotation = OriginRotation * quaternion.LookRotation(position, Up);
            Matrices[i] = float4x4.TRS(position, rotation, Scale);
        }
    }

    public static class UnityMathematicsExtensionMethods
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x4 Scaled(this float4x4 m, float3 scale)
        {
            m.c0[0] = scale.x;
            m.c1[1] = scale.y;
            m.c2[2] = scale.z;
            return m;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x4 TranslatePosition(this float4x4 m, float4 trans)
        {
            m.c3 += trans;
            return m;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetScale(this float4x4 m)
        {
            return new float3(m.c0[0], m.c1[1], m.c2[2]);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetScale(this ref float4x4 m, float3 scale)
        {
            m.c0[0] = scale.x;
            m.c1[1] = scale.y;
            m.c2[2] = scale.z;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetPosition(this float4x4 m)
        {
            var p = m.c3;
            return new float3(p.x, p.y, p.z);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x4 SetPosition(this float4x4 m, float3 position)
        {
            m.c3 = new float4(position, 0f);
            return m;
        }
    }
}
