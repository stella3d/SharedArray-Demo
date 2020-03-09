using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class InstanceMeshDrawer : MonoBehaviour
{
    [Range(0, 1023 * 16)]
    public int Count;
    
    public Material Material;
    public Mesh Mesh;

    public ManagedNativeArray<Matrix4x4>[] Matrices;
    public Color[][] Colors;
    
    static readonly int ColorShaderProperty = Shader.PropertyToID("_Color");

    MaterialPropertyBlock[] m_PropertyBlocks;

    bool m_ColorsDirty;

    void Awake()
    {
        Material.enableInstancing = true;
        Initialize();
    }

    void Initialize()
    { 
        var wholeBatchCount = Count / 1023;
        var remainder = Count % 1023;

        var batchCount = remainder == 0 ? wholeBatchCount : wholeBatchCount + 1;

        Matrices = new ManagedNativeArray<Matrix4x4>[batchCount];
        Colors = new Color[batchCount][];
        
        m_PropertyBlocks = new MaterialPropertyBlock[batchCount];
        
        m_Handles = new NativeArray<JobHandle>(batchCount, Allocator.Persistent);
        
        for (int i = 0; i < wholeBatchCount; i++)
        {
            Matrices[i] = new ManagedNativeArray<Matrix4x4>(RandomMatrices(1023, 8f + i * 2f));
            var colors = RandomColors(1023);
            Colors[i] = colors;
            
            m_PropertyBlocks[i] = new MaterialPropertyBlock();
            m_PropertyBlocks[i].SetVectorArray(ColorShaderProperty, FromColors(colors));
        }
        
        if(remainder != 0)
        {
            var index = batchCount - 1;
            Matrices[index] = new ManagedNativeArray<Matrix4x4>(RandomMatrices(remainder, (wholeBatchCount + 1) * 2f + 8f));
            var colors = RandomColors(remainder);
            Colors[index] = colors;
            
            m_PropertyBlocks[index] = new MaterialPropertyBlock();
            m_PropertyBlocks[index].SetVectorArray(ColorShaderProperty, FromColors(colors));
        }
    }

    JobHandle m_Handle;

    NativeArray<JobHandle> m_Handles;

    void Update()
    {
        m_Handle.Complete();

        // draw
        for (int i = 0; i < m_PropertyBlocks.Length; i++)
        {
            var matrices = Matrices[i];
            Graphics.DrawMeshInstanced(Mesh, 0, Material, matrices, matrices.Length, m_PropertyBlocks[i]);
        }
        
        // schedule movement update jobs
        for (int i = 0; i < m_PropertyBlocks.Length; i++)
        {
            var array = Matrices[i];
            var job = new ExampleNoiseJob
            {
                Matrices = array, 
                SinTime = 0.005f * math.sin(Time.time)
            };
            var handle = job.Schedule(array.Length, array.Length, m_Handle);
            m_Handles[i] = handle;
        }

        m_Handle = JobHandle.CombineDependencies(m_Handles);
    }

    static Vector4[] FromColors(Color[] colors)
    {
        var vectors = new Vector4[colors.Length];
        for (int i = 0; i < vectors.Length; i++)
            vectors[i] = colors[i];

        return vectors;
    }

    static Matrix4x4[] RandomMatrices(int count, float scale = 15f)
    {
        var matrices = new Matrix4x4[count];
        for (int i = 0; i < matrices.Length; i++)
        {
            var pos = Random.onUnitSphere * scale;
            var rotation = Quaternion.LookRotation(pos, Vector3.up);
            matrices[i] = Matrix4x4.TRS(pos, rotation, Vector3.one);
        }

        return matrices;
    }
    
    static Color[] RandomColors(int count)
    {
        var colors = new Color[count];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0f, 1f, 0.2f, 0.8f);
        }
        
        return colors;
    }
}
