# SharedArray Demo

A Unity project that visually demonstrates the use of the [SharedArray library](https://github.com/stella3d/SharedArray), 
to achieve more efficient integration with Unity's C# job system and newer math types.


![Thousands of randomly colored, bright cubes rendered in a layered sphere](readme_image.png)

## Running

Clone this repo & open it as a project in Unity 2019.3+.

To run the demo, open the `SharedArray Demo` scene, inspect the `Demo Mesh Drawer` object in the scene, and click ▶.


### What It Does

Arbitrary calculations for changing the position & color of mesh instances are performed, each frame. 

These are done on worker threads, in [C# jobs](https://docs.unity3d.com/Manual/JobSystem.html), using [Unity.Mathematics](https://github.com/Unity-Technologies/Unity.Mathematics) types & the [Burst compiler](https://docs.unity3d.com/Packages/com.unity.burst@1.1/manual/index.html).

After the calculations are complete for a frame, we draw the updated mesh instances. 

The same data / memory used in the calculation jobs is used as a normal array on the main thread, without copying.

This is so we can use that data as arguments to Unity methods like `Graphics.DrawMeshInstanced` that take normal C# arrays.
