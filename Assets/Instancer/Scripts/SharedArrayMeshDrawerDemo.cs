using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stella3D
{
    public class SharedArrayMeshDrawerDemo : MonoBehaviour
    {
        const int instanceBatchSize = 1023;
        static readonly int ColorShaderProperty = Shader.PropertyToID("_Color");
        static readonly ProfilerMarker DrawProfileMarker = new ProfilerMarker("Draw Mesh Instanced");
        
        [Range(instanceBatchSize, instanceBatchSize * 49)]
        [Tooltip("The number of instances of the mesh to draw")]
        public int InstanceCount = instanceBatchSize * 4;

        [Range(0.25f, 25f)]
        [Tooltip("The strength of the noise")]
        public float NoiseScale = 10;

        [Range(0.0002f, 0.02f)] 
        public float DistanceScale = 0.005f;
        
        [Range(0.001f, 0.01f)] 
        public float ColorScale = 0.004f;
        
        [Range(0.05f, 2f)] 
        public float CycleTimeScale = 0.25f;
        
        public Material Material;
        public Mesh Mesh;

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
            
            DrawProfileMarker.Begin();
            var isEvenFrame = Time.frameCount % 2 == 0;

            // draw using the newly updated data from jobs
            for (int i = 0; i < m_PropertyBlocks.Length; i++)
            {
                var block = m_PropertyBlocks[i];
                // implicit conversion from ManagedNativeArray<T> to T[] performs a
                // read and write safety check, ensuring that no job is reading or writing to the data
                if (!isEvenFrame)
                    block.SetVectorArray(ColorShaderProperty, Colors[i]);

                var matrices = Matrices[i];
                // The same data used in jobs as a NativeArray<float4> is used as a Vector4[] here
                Graphics.DrawMeshInstanced(Mesh, 0, Material, matrices, matrices.Length, block, 
                    ShadowCastingMode.Off, false);
            }

            DrawProfileMarker.End();
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
                // the ManagedNativeArray<Matrix4x4> 'array' is directly used as a NativeArray<Matrix4x4> with jobs
                var job = new ExampleNoiseJob(array, sinTime, NoiseScale);
                m_Handles[i] = job.Schedule(array.Length, m_JobHandle);
            }

            return JobHandle.CombineDependencies(m_Handles);
        }

        JobHandle ScheduleColorJobs()
        {
            var sinTime = ColorScale * math.cos(Time.time * CycleTimeScale * 0.5f);

            for (int i = 0; i < Colors.Length; i++)
            {
                var colors = Colors[i];
                // the ManagedNativeArray<Color> 'colors' is directly used as a NativeArray<Color> with jobs
                var job = new ColorShiftJob(colors, sinTime);
                m_Handles[i] = job.Schedule(colors.Length, m_JobHandle);
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
            
            // for each full batch of 1023
            for (int i = 0; i < wholeBatchCount; i++)
                InitializeIndex(instanceBatchSize, i);
            
            // the last batch, if the count isn't divisible by 1023
            if(remainder != 0)
                InitializeIndex(remainder, batchCount - 1);
        }

        void InitializeIndex(int count, int index)
        {
            var matrices = Utils.RandomMatrices(count, index * 10f + 12f);
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

