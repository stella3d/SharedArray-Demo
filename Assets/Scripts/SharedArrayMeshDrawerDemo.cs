using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stella3D.SharedArray.Demo
{
    public class SharedArrayMeshDrawerDemo : MonoBehaviour
    {
        const int instanceBatchSize = 1023;
        static readonly int ColorShaderProperty = Shader.PropertyToID("_Color");
        static readonly ProfilerMarker DrawProfileMarker = new ProfilerMarker("Draw Mesh Instanced");
        static readonly ProfilerMarker SetVectorArrayMarker = new ProfilerMarker("MaterialPropertyBlock.SetVectorArray");
        
        [Header("Drawing Parameters")]
        [Tooltip("The mesh to draw instances of")]
        public Mesh Mesh;

        [Tooltip("The material to draw the mesh with.\nMust support instancing!")]
        public Material Material;

        [Tooltip("Number of mesh instances to draw.\nNo effect after startup.\nIncrements of 1023")]
        [Range(instanceBatchSize, instanceBatchSize * 49)]
        public int InstanceCount = instanceBatchSize * 4;

        [Header("Effect Scaling")]
        [Tooltip("Affects the strength of the noise")]
        [Range(0.25f, 25f)]
        public float NoiseScale = 10;

        [Tooltip("Affects how far meshes move")]
        [Range(0.0002f, 0.02f)]
        public float DistanceScale = 0.005f;
        
        [Tooltip("Affects how much effect the color shifting has")]
        [Range(0.001f, 0.005f)]
        public float ColorScale = 0.004f;
        
        [Tooltip("Affects how fast the time cycle goes by")]
        [Range(0.05f, 2f)]
        public float CycleTimeScale = 0.25f;

        /* for every instance of a mesh drawn with the instanced drawer,
           we need a Matrix4x4 and Color (from UnityEngine).
           
           We want to manipulate these values inside a C# job, before using in graphics APIs that take a regular array.
           
           In jobs, we want to take advantage of Unity.Mathematics optimizations by working only with those types.
           
           The second type argument to each array is the type to create the NativeArray representation as. 
           this allows aliasing to element types that are the same size as the source type.
        */
        public SharedArray<Matrix4x4, float4x4>[] Matrices;
        public SharedArray<Vector4, float4>[] Colors;

        MaterialPropertyBlock[] m_PropertyBlocks;
        
        JobHandle m_JobHandle;
        NativeArray<JobHandle> m_Handles;

        void Awake()
        {
            try
            {
                Material.enableInstancing = true;
            }
            catch (Exception)
            {
                Debug.LogError("Material must support instancing!");
                enabled = false;
                return;
            }

            Initialize();
        }

        void Update()
        {
            // complete all jobs scheduled at the end of the previous Update()
            // this will make the data safe to read and write according to Unity's job safety system
            m_JobHandle.Complete();
            
            var isEvenFrame = Time.frameCount % 2 == 0;

            // draw using the newly updated data from jobs
            for (int i = 0; i < m_PropertyBlocks.Length; i++)
            {
                var block = m_PropertyBlocks[i];

                if (!isEvenFrame)
                {
                    SetVectorArrayMarker.Begin();
                    // implicit conversion from SharedArray<Vector4, float4> to Vector4[] in the 2nd argument
                    // performs a safety check, ensuring that no job is reading or writing to the data
                    block.SetVectorArray(ColorShaderProperty, Colors[i]);
                    SetVectorArrayMarker.End();
                }

                var matrices = Matrices[i];

                DrawProfileMarker.Begin();
                // The same SharedArray used in the 'position noise' jobs as a NativeArray<float4x4> 
                // is used implicitly as a Matrix4x4[] here in the method arguments
                Graphics.DrawMeshInstanced(Mesh, 0, Material, matrices, matrices.Length, block, ShadowCastingMode.Off, false);
                DrawProfileMarker.End();
            }

            // even frames, update colors, odd frames update positions
            m_JobHandle = isEvenFrame ? ScheduleColorJobs() : SchedulePositionNoiseJobs();
            // if you try to access the managed array representation of the ManagedNativeArray here,
            // after jobs are scheduled, an exception is thrown because uncompleted jobs are writing to the data
        }

        JobHandle SchedulePositionNoiseJobs()
        {
            var sinTime = DistanceScale * math.sin(Time.time * CycleTimeScale);
            
            for (int i = 0; i < Matrices.Length; i++)
            {
                var array = Matrices[i];
                // the SharedArray<Matrix4x4, float4x4> 'array' is directly used as a NativeArray<Matrix4x4> with jobs
                var job = new ExampleNoiseJob(array, sinTime, NoiseScale);
                m_Handles[i] = job.Schedule(array.Length, 512, m_JobHandle);
            }

            return JobHandle.CombineDependencies(m_Handles);
        }

        JobHandle ScheduleColorJobs()
        {
            var sinTime = ColorScale * math.cos(Time.time * CycleTimeScale * 0.5f);

            for (int i = 0; i < Colors.Length; i++)
            {
                var colors = Colors[i];
                // the SharedArray<Vector4, float4> 'colors' is directly used as a NativeArray<float4> with jobs
                var job = new ColorShiftJob(colors, sinTime);
                m_Handles[i] = job.Schedule(colors.Length, 512, m_JobHandle);
            }
            
            return JobHandle.CombineDependencies(m_Handles);
        }
        
        void Initialize()
        { 
            var wholeBatchCount = InstanceCount / instanceBatchSize;
            var remainder = InstanceCount % instanceBatchSize;
            var batchCount = remainder == 0 ? wholeBatchCount : wholeBatchCount + 1;

            Matrices = new SharedArray<Matrix4x4, float4x4>[batchCount];
            Colors = new SharedArray<Vector4, float4>[batchCount];
            
            m_Handles = new NativeArray<JobHandle>(batchCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_PropertyBlocks = new MaterialPropertyBlock[batchCount];

            var position = transform.position;
            // for each full batch of 1023
            for (int i = 0; i < wholeBatchCount; i++)
                InitializeIndex(position, instanceBatchSize, i);
            
            // the last batch, if the count isn't divisible by 1023
            if(remainder != 0)
                InitializeIndex(position, remainder, batchCount - 1);
        }

        void InitializeIndex(Vector3 center, int count, int index)
        {
            var matrices = Utils.RandomMatrices(center, count, index * 10f + 18f);
            Matrices[index] = new SharedArray<Matrix4x4, float4x4>(matrices);
            var colors = new SharedArray<Vector4, float4>(Utils.RandomColors(count));
            Colors[index] = colors;
            
            var block = new MaterialPropertyBlock();
            block.SetVectorArray(ColorShaderProperty, colors);
            m_PropertyBlocks[index] = block;
        }

        void OnValidate()
        {
            // align instance count to max batch size for DrawMeshInstanced
            var remainder = InstanceCount % instanceBatchSize;
            if (remainder != 0)
                InstanceCount -= remainder;
        }
        
        void OnDestroy()
        {
            if(!m_JobHandle.IsCompleted) m_JobHandle.Complete();
            if (m_Handles.IsCreated) m_Handles.Dispose();
        }
    }
}
