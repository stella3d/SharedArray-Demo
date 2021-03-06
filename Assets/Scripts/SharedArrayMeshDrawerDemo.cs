﻿using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stella3D.Demo
{
    public class SharedArrayMeshDrawerDemo : MonoBehaviour
    {
        /*
           For every mesh instance to draw, we have a Matrix4x4 and Color
           
           In c# jobs, we want to take advantage of Unity.Mathematics optimizations in Burst,
           by working only with the math types in that namespace.
           
           On the main thread, we want to access the data as a Matrix4x4[] or Color[].   

           To do this without copy/casting overhead, we "alias" a SharedArray's memory as a NativeArray
           of a different struct type, of the same size.

           'UnityEngine.Vector4[]' <-> 'NativeArray<Unity.Mathematics.float4>', 
           'UnityEngine.Matrix4x4[]' <-> 'NativeArray<Unity.Mathematics.float4x4>'. 
        */

        /// <summary>Transform matrix for every mesh instance, 1023 per array</summary>
        public SharedArray<Matrix4x4, float4x4>[] Matrices;
        
        /// <summary>Color for every mesh instance, 1023 per array</summary>
        public SharedArray<Vector4, float4>[] Colors;

        /// <summary>MaterialPropertyBlock for every mesh instance, 1023 per array</summary>
        public MaterialPropertyBlock[] PropertyBlocks;
                
        [Header("Mesh Parameters")]
        [Tooltip("The mesh to draw instances of")]
        public Mesh Mesh;

        [Tooltip("The material to draw the mesh with.\nMust support instancing!")]
        public Material Material;

        [Tooltip("Number of mesh instances to draw.\nNo effect after startup.\nIncrements of 1023")]
        [Range(instanceBatchSize, instanceBatchSize * 49)]
        public int InstanceCount = instanceBatchSize * 4;

        [Header("Effect Scaling")]
        [Tooltip("Affects the strength of the noise")]
        [Range(0.35f, 35f)]
        public float NoiseScale = 10f;

        [Tooltip("Affects how far meshes move")]
        [Range(0.02f, 1.6f)]
        public float DistanceScale = 0.5f;
        
        [Tooltip("Affects how much effect the color shifting has")]
        [Range(0.2f, 2f)]
        public float ColorScale = 1f;
        
        [Tooltip("Affects how fast the time cycle goes by")]
        [Range(0.1f, 4f)]
        public float CycleTimeScale = 2f;
        
        const int instanceBatchSize = 1023;
        static readonly int ColorShaderProperty = Shader.PropertyToID("_Color");
        static readonly ProfilerMarker DrawProfileMarker = new ProfilerMarker("Draw Mesh Instanced");
                
        JobHandle m_JobHandle;
        NativeArray<JobHandle> m_Handles;
        
        float m_CycleTimeMultiplier;
        float m_PreviousCycleTimeScale;
        Vector3 m_PreviousScale;
        Vector3 m_StartingCenter;
        Vector3 m_PreviousPosition;
        Quaternion m_PreviousRotation;

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
            // this will make the data safe to read and write, according to Unity's job safety system
            m_JobHandle.Complete();
            
            var isOddFrame = Time.frameCount % 2 != 0;
            // draw using the newly updated data from jobs
            for (int i = 0; i < PropertyBlocks.Length; i++)
            {
                var block = PropertyBlocks[i];
                if (isOddFrame)
                {
                    // Implicit conversion from SharedArray<Vector4, float4> to Vector4[]
                    // In the editor, this cast does a safety check, ensuring no job is reading/writing the memory
                    Vector4[] colors = Colors[i];
                    block.SetVectorArray(ColorShaderProperty, colors);
                }

                SharedArray<Matrix4x4, float4x4> matrices = Matrices[i];

                DrawProfileMarker.Begin();
                // SharedArrays used in position calculation jobs as NativeArray<float4x4>
                // are used implicitly as Matrix4x4[] here
                Graphics.DrawMeshInstanced(Mesh, 0, Material, matrices, matrices.Length, block, ShadowCastingMode.Off, false);
                DrawProfileMarker.End();
            }

            // if the transform has changed, update transforms of drawn meshes
            m_JobHandle = ScheduleTransformUpdateJobsIfNeeded();
            // schedule position calculation on odd frames, colors on even frames
            m_JobHandle = isOddFrame ? SchedulePositionNoiseJobs() : ScheduleColorJobs();

            // If in editor / using safety system:
            // if you tried to cast any of the SharedArrays in 'Matrices' or 'Colors' to a plain array here,
            // an exception would be thrown - because uncompleted jobs have been scheduled that read and write the data.
        }

        JobHandle SchedulePositionNoiseJobs()
        {
            var actualScale = DistanceScale * 0.01f;
            var sinTime = actualScale * math.sin(Time.time * m_CycleTimeMultiplier);
            
            for (int i = 0; i < Matrices.Length; i++)
            {
                var array = Matrices[i];
                // the SharedArray<Matrix4x4, float4x4> 'array' is directly used as a NativeArray<Matrix4x4> with jobs
                var job = new PositionNoiseJob(array, sinTime, NoiseScale);
                m_Handles[i] = job.Schedule(array.Length, 512, m_JobHandle);
            }

            return JobHandle.CombineDependencies(m_Handles);
        }

        JobHandle ScheduleColorJobs()
        {
            var actualScale = ColorScale * (1f / 400f);
            var sinTime = actualScale * math.sin(Time.time * m_CycleTimeMultiplier * 0.5f);

            for (int i = 0; i < Colors.Length; i++)
            {
                var colors = Colors[i];
                // the SharedArray<Vector4, float4> 'colors' is directly used as a NativeArray<float4> with jobs
                var job = new ColorShiftJob(colors, sinTime);
                m_Handles[i] = job.Schedule(colors.Length, 512, m_JobHandle);
            }
            
            return JobHandle.CombineDependencies(m_Handles);
        }


        JobHandle ScheduleTransformUpdateJobsIfNeeded()
        {
            var trans = transform;
            var newScale = trans.lossyScale;
            var newPosition = trans.position;
            var newRotation = trans.rotation;

            var scaleChanged = m_PreviousScale != newScale;
            var positionChanged = m_PreviousPosition != newPosition;
            var rotationChanged = m_PreviousRotation != newRotation;

            if (positionChanged)
            {
                var diffFromPrevious = newPosition - m_PreviousPosition;
                for (int i = 0; i < Matrices.Length; i++)
                {
                    var matrices = Matrices[i];
                    if (scaleChanged)
                    {
                        // the SharedArray<Matrix4x4, float4x4> 'array' is directly used as a NativeArray<Matrix4x4> with jobs
                        var job = new TransformUpdateJob(matrices, newScale, diffFromPrevious);
                        m_Handles[i] = job.Schedule(matrices.Length, m_JobHandle);
                    }
                    else
                    {
                        var job = new TranslatePositionJob(matrices, diffFromPrevious);
                        m_Handles[i] = job.Schedule(matrices.Length, m_JobHandle);
                    }
                }

                m_JobHandle = JobHandle.CombineDependencies(m_Handles);
            }
            else if (scaleChanged)   
            {
                for (int i = 0; i < Matrices.Length; i++)
                {
                    var matrices = Matrices[i];
                    var job = new ScaleUpdateJob(matrices, newScale);
                    m_Handles[i] = job.Schedule(matrices.Length, m_JobHandle);
                }

                m_JobHandle = JobHandle.CombineDependencies(m_Handles);
            }
            else if (rotationChanged)
            {
                var rotationDiff = m_PreviousRotation * newRotation;
                for (int i = 0; i < Matrices.Length; i++)
                {
                    var matrices = Matrices[i];
                    var job = new OriginRotationUpdateJob(matrices, newScale, rotationDiff);
                    m_Handles[i] = job.Schedule(matrices.Length, m_JobHandle);
                }
                
                m_JobHandle = JobHandle.CombineDependencies(m_Handles);
            }

            m_PreviousScale = newScale;
            m_PreviousPosition = newPosition;
            m_PreviousRotation = newRotation;
            return m_JobHandle;
        }
        
        void Initialize()
        {
            var trans = transform;
            var position = trans.position;
            
            m_StartingCenter = position;
            m_CycleTimeMultiplier = 1f / CycleTimeScale;
            var wholeBatchCount = InstanceCount / instanceBatchSize;
            var remainder = InstanceCount % instanceBatchSize;
            var batchCount = remainder == 0 ? wholeBatchCount : wholeBatchCount + 1;

            Matrices = new SharedArray<Matrix4x4, float4x4>[batchCount];
            Colors = new SharedArray<Vector4, float4>[batchCount];
            
            m_Handles = new NativeArray<JobHandle>(batchCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            PropertyBlocks = new MaterialPropertyBlock[batchCount];

            var scale = trans.lossyScale;
            // for each full batch of 1023
            for (int i = 0; i < wholeBatchCount; i++)
                InitializeIndex(position, scale, instanceBatchSize, i);
            
            // the last batch, if the count isn't divisible by 1023
            if(remainder != 0)
                InitializeIndex(position, scale, remainder, batchCount - 1);
        }

        void InitializeIndex(Vector3 center, Vector3 scale, int count, int index)
        {
            var matrices = RandomUtils.Matrices(center, scale, count, index * 18f + 20f);
            Matrices[index] = new SharedArray<Matrix4x4, float4x4>(matrices);
            var colors = new SharedArray<Vector4, float4>(RandomUtils.Colors(count));
            Colors[index] = colors;
            
            var block = new MaterialPropertyBlock();
            block.SetVectorArray(ColorShaderProperty, colors);
            PropertyBlocks[index] = block;
        }

        void OnValidate()
        {
            // align instance count to max batch size for DrawMeshInstanced
            var remainder = InstanceCount % instanceBatchSize;
            if (remainder != 0)
                InstanceCount -= remainder;

            // keep cycle time multiplier updated if inspector changes value
            if (m_PreviousCycleTimeScale != CycleTimeScale)
            {
                m_CycleTimeMultiplier = 1f / CycleTimeScale;
                m_PreviousCycleTimeScale = CycleTimeScale;
            }
        }
        
        void OnDestroy()
        {
            if(!m_JobHandle.IsCompleted) m_JobHandle.Complete();
            if (m_Handles.IsCreated) m_Handles.Dispose();
        }

        void OnDisable()
        {
            if(!m_JobHandle.IsCompleted) m_JobHandle.Complete();
            if (m_Handles.IsCreated) m_Handles.Dispose();
        }
    }
}
