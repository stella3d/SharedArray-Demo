using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public class ManagedNativeArray<T> : IDisposable
    where T: unmanaged
{
    T[] m_Managed;
    NativeArray<T> m_Native;
    GCHandle m_GcHandle;
    
    public T[] Managed => m_Managed;
    public NativeArray<T> Native => m_Native;

    public int Length => m_Managed.Length;

    public ManagedNativeArray(T[] managed)
    {
        m_Managed = managed;
        
        // Unity's garbage collector doesn't move objects around, so this should not even be necessary.
        // there's not much downside to playing it safe, though
        m_GcHandle = GCHandle.Alloc(Managed, GCHandleType.Pinned);
        
        InitializeNative();
    }

    public static implicit operator T[](ManagedNativeArray<T> self)
    {
        return self.m_Managed;
    }
    
    public static implicit operator NativeArray<T>(ManagedNativeArray<T> self)
    {
        return self.m_Native;
    }
    
    unsafe void InitializeNative()
    {
        fixed (void* ptr = m_Managed)
        {
            m_Native = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, m_Managed.Length, Allocator.None);
        }
        
#if UNITY_EDITOR      
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref m_Native, AtomicSafetyHandle.Create());
#endif
    }
    
    public void Resize(int newSize)
    {
        if (newSize == Managed.Length)
            return;
        
        if(m_GcHandle.IsAllocated) m_GcHandle.Free();
        Array.Resize(ref m_Managed, newSize);
        m_GcHandle = GCHandle.Alloc(Managed, GCHandleType.Pinned);
        
        if(Native.IsCreated) Native.Dispose();
        InitializeNative();
    }

    public void Dispose()
    {
        if(m_GcHandle.IsAllocated) m_GcHandle.Free();
        if(Native.IsCreated) Native.Dispose();
    }
}
