using Sirenix.OdinInspector;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

using UnityEditor;

using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

using WhiteboardGames.Utilities;
using WhiteboardGames.Utilities.Physical;

using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

public class Voxel : MonoBehaviour
{
    // https://bronsonzgeb.com/index.php/2021/05/22/gpu-mesh-voxelizer-part-1/

    [SerializeField]
    [Min(0.0f)]
    private float voxelSize = 1.0f;

    [SerializeField]
    private Vector3 drawOffset;

    private NativeList<bool> voxels;
    private VoxelizationInfo info;
    private bool isWorking;

    private void OnDrawGizmos()
    {
        if (voxels.IsCreated)
        {
            VoxelizationInfo info = this.info;

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube((info.Min + info.Max) / 2.0f, info.Max - info.Min);

            Gizmos.color = Color.green;

            float voxelSize = info.VoxelSize;
            int3 cells = info.Cells;
            float3 offset = info.Min + voxelSize + (float3)drawOffset;
            NativeArray<bool> voxels = this.voxels.AsArray();
            for (int x = 0; x < cells.x; x++)
            {
                for (int y = 0; y < cells.y; y++)
                {
                    for (int z = 0; z < cells.z; z++)
                    {
                        if (voxels[GetIndex(new(x, y, z), cells)])
                        {
                            float3 centerPos = (voxelSize * new float3(x, y, z)) + offset;
                            Gizmos.DrawWireCube(centerPos, Vector3.one * voxelSize);
                        }
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (voxels.IsCreated)
        {
            voxels.Dispose();
        }        
    }

    private struct VoxelizationCoroutine : CoroutineManager.ICoroutine
    {
        private readonly Voxel self;

        private int state;

        private int i;
        private int j;

        private MeshFilter[] meshFilters;
        private TransformAccessArray transformAccessArray;
        private List<Mesh> meshes;
        private NativeArray<Vector3> lossyScales;
        private Mesh.MeshDataArray meshDataArray;
        private NativeArray<VoxelizationInfo> info;

        private JobHandle jobHandle;

        public VoxelizationCoroutine(Voxel self)
        {
            this = default;
            this.self = self;
        }

        public bool MoveNext()
        {
            switch (state)
            {
                case 0:
                {
                    state = 1;
                    meshFilters = self.gameObject.GetComponentsInChildren<MeshFilter>();
                    return true;
                }
                case 1:
                {
                    state = 2;
                    int length = meshFilters.Length;
                    transformAccessArray = new(length);
                    meshes = new(length);
                    lossyScales = new(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    return true;
                }
                case 2:
                {
                    MeshFilter[] meshFilters = this.meshFilters;
                    int length = meshFilters.Length;
                    while (i < length)
                    {
                        MeshFilter meshFilter = meshFilters[i++];
                        GameObject gameObject = meshFilter.gameObject;
                        if (!gameObject.activeInHierarchy)
                        {
                            return true;
                        }

                        meshes.Add(meshFilter.sharedMesh);
                        Transform transform = gameObject.transform;
                        DevDebug.Assert(transform != null);
                        transformAccessArray.Add(transform);
                        lossyScales[j++] = transform.lossyScale;
                        return true;
                    }
                    state = 3;
                    return true;
                }
                case 3:
                {
                    state = 4;
                    meshDataArray = Mesh.AcquireReadOnlyMeshData(meshes);
                    return true;
                }
                case 4:
                {
                    state = 5;
                    NativeArray<(float3 Min, float3 Max)> bounds = new(i, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    JobHandle jobHandle = new GenerateBoundingBoxJob(meshDataArray, lossyScales, bounds).ScheduleReadOnly(transformAccessArray, 1);
                    NativeArray<VoxelizationInfo> info = this.info = new(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    info[0] = new() { VoxelSize = self.voxelSize, HalfVoxelSize = self.voxelSize * 0.5f };
                    jobHandle = new MergeBoundingBoxesJob(bounds, info).Schedule(jobHandle);

                    if (!self.voxels.IsCreated)
                    {
                        self.voxels = new(0, Allocator.Persistent);
                    }
                    else
                    {
                        self.voxels.Clear();
                    }

                    jobHandle = new PrepareToVoxelizeJob(self.voxels, info).Schedule(jobHandle);
                    jobHandle = new VoxelizeJob(meshDataArray, lossyScales, bounds, self.voxels.AsDeferredJobArray(), info).ScheduleReadOnly(transformAccessArray, 1, jobHandle);
                    this.jobHandle = JobHandle.CombineDependencies(lossyScales.Dispose(jobHandle), bounds.Dispose(jobHandle));

                    return true;
                }
                case 5 when jobHandle.IsCompleted:
                {
                    state = 6;

                    jobHandle.Complete();
                    NativeArray<VoxelizationInfo> info = this.info;
                    self.info = info[0];
                    info.Dispose();

                    return false;
                }
            }
            return false;
        }

        public void MoveAll()
        {
            while (MoveNext()) ;
        }
    }

    [Button]
    private void Voxelize()
    {
        if (isWorking)
        {
            return;
        }

        isWorking = true;
        VoxelizationCoroutine coroutine = new(this);
        EditorApplication.CallbackFunction q = null;
        System.Diagnostics.Stopwatch s = System.Diagnostics.Stopwatch.StartNew();
        q = () =>
        {
            s.Restart();
            do
            {
                if (!coroutine.MoveNext())
                {
                    EditorApplication.update -= q;
                    isWorking = false;
                    Debug.Log("Completed");
                    return;
                }
            }
            while (s.ElapsedMilliseconds < 5);
        };
        EditorApplication.update += q;
    }

    private struct VoxelizationInfo
    {
        public float VoxelSize;
        public float HalfVoxelSize;
        public float3 Min;
        public float3 Max;
        public int3 Cells;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetIndex(int3 cords, int3 cells)
    {
        return cords.x + (cells.x * (cords.y + (cells.y * cords.z)));
    }

    //[BurstCompile]
    private struct GenerateBoundingBoxJob : IJobParallelForTransform
    {
        [ReadOnly]
        private Mesh.MeshDataArray meshDataArray;

        [ReadOnly]
        private NativeArray<Vector3> lossyScales;

        [WriteOnly]
        private NativeArray<(float3 Min, float3 Max)> bounds;

        public GenerateBoundingBoxJob(Mesh.MeshDataArray meshDataArray, NativeArray<Vector3> lossyScales, NativeArray<(float3 Min, float3 Max)> bounds)
        {
            this.meshDataArray = meshDataArray;
            this.lossyScales = lossyScales;
            this.bounds = bounds;
        }

        public unsafe void Execute(int index, TransformAccess transform)
        {
            Mesh.MeshData meshData = meshDataArray[index];

            int vertexCount = meshData.vertexCount;
            NativeArray<Vector3> vertex = new(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            meshData.GetVertices(vertex);

            transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
            TransformInfo transformInfo = new(*(float3*)&position, *(quaternion*)&rotation, *(float3*)&((Vector3*)lossyScales.GetUnsafeReadOnlyPtr())[index]);

            float3 min = float.MaxValue;
            float3 max = float.MinValue;
            for (int i = 0; i < vertexCount; i++)
            {
                float3 vertice = *(float3*)&((Vector3*)vertex.GetUnsafeReadOnlyPtr())[i];
                float3 worldVertice = transformInfo.TransformPoint(vertice);

                float3 a = math.min(min, worldVertice);
                float3 b = math.max(max, worldVertice);

                // We use math.min and math.Max methods because Size or LossyScale may have negative values in some of its axis.
                min = math.min(a, b);
                max = math.max(a, b);
            }

            bounds[index] = (min, max);
            vertex.Dispose();
        }
    }

    //[BurstCompile]
    private struct MergeBoundingBoxesJob : IJob
    {
        [ReadOnly]
        private NativeArray<(float3 Min, float3 Max)> bounds;

        private NativeArray<VoxelizationInfo> info;

        public MergeBoundingBoxesJob(NativeArray<(float3 Min, float3 Max)> bounds, NativeArray<VoxelizationInfo> info)
        {
            this.bounds = bounds;
            this.info = info;
        }

        public void Execute()
        {
            float3 min = float.MaxValue;
            float3 max = float.MinValue;
            NativeArray<(float3 Min, float3 Max)> bounds = this.bounds;
            for (int i = 0; i < bounds.Length; i++)
            {
                (float3 Min, float3 Max) value = bounds[i];
                min = math.min(min, value.Min);
                max = math.max(max, value.Max);
            }

            VoxelizationInfo info_ = info[0];
            info_.Min = min;
            info_.Max = max;
            info[0] = info_;
        }
    }

    //[BurstCompile]
    private struct VoxelizeJob : IJobParallelForTransform
    {
        [ReadOnly]
        private Mesh.MeshDataArray meshDataArray;

        [ReadOnly]
        private NativeArray<Vector3> lossyScales;

        [ReadOnly]
        private NativeArray<(float3 Min, float3 Max)> individualBounds;

        [ReadOnly]
        private NativeArray<VoxelizationInfo> info;

        private NativeArray<bool> voxels;

        public VoxelizeJob(Mesh.MeshDataArray meshDataArray, NativeArray<Vector3> lossyScales, NativeArray<(float3 Min, float3 Max)> individualBounds, NativeArray<bool> voxels, NativeArray<VoxelizationInfo> info)
        {
            this.meshDataArray = meshDataArray;
            this.lossyScales = lossyScales;
            this.individualBounds = individualBounds;
            this.voxels = voxels;
            this.info = info;
        }

        public unsafe void Execute(int index, TransformAccess transform)
        {
            Mesh.MeshData meshData = meshDataArray[index];

            transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
            TransformInfo transformInfo = new(*(float3*)&position, *(quaternion*)&rotation, *(float3*)&((Vector3*)lossyScales.GetUnsafeReadOnlyPtr())[index]);

            VoxelizationInfo info_ = info[0];

            (float3 Min, float3 Max) bounds = individualBounds[index];
            Debug.Assert(math.all((bounds.Min >= info_.Min) & (bounds.Max <= info_.Max)), "Bounds out of range.");
            int3 min = (int3)math.floor((bounds.Min - info_.Min) / info_.VoxelSize);
            int3 max = (int3)math.floor((bounds.Max - info_.Min) / info_.VoxelSize);
            Debug.Assert(math.all(min >= 0 & max <= info_.Cells), "Bounds out of range.");

            NativeArray<Vector3> vertex = new(meshData.vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            meshData.GetVertices(vertex);

            int subMeshCount = meshData.subMeshCount;
            for (int i = 0; i < subMeshCount; i++)
            {
                SubMeshDescriptor subMesh = meshData.GetSubMesh(i);
                Debug.Assert(subMesh.topology == MeshTopology.Triangles, "Only support meshes with triangle topolgy.");
                NativeArray<int> indices = new(subMesh.indexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                meshData.GetIndices(indices, i);

                for (int x = min.x; x < max.x; x++)
                {
                    for (int y = min.y; y < max.y; y++)
                    {
                        for (int z = min.z; z < max.z; z++)
                        {
                            VoxelizeCell(ref info_, vertex, indices, ref transformInfo, new(x, y, z));
                        }
                    }
                }

                indices.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void VoxelizeCell(ref VoxelizationInfo info, NativeArray<Vector3> vertex, NativeArray<int> indices, ref TransformInfo transformInfo, int3 cords)
        {
            float halfVoxelSize = info.HalfVoxelSize;
            float3 centerPos = (info.VoxelSize * (float3)cords) + halfVoxelSize + info.Min;
            float3 aabbCenter = centerPos;
            float3 aabbExtents = halfVoxelSize;

            int raycastIntersections = 0;
            for (int j = 0; j < indices.Length; j += 3)
            {
                int a = indices[j];
                int b = indices[j + 1];
                int c = indices[j + 2];

                Vector3 triA_ = vertex[a];
                Vector3 triB_ = vertex[b];
                Vector3 triC_ = vertex[c];

                float3 triA = transformInfo.TransformPoint(*(float3*)&triA_);
                float3 triB = transformInfo.TransformPoint(*(float3*)&triB_);
                float3 triC = transformInfo.TransformPoint(*(float3*)&triC_);

                // Check if any of the cube's edge intersects with any triangle.
                if (IntersectsTriangleAABB(triA, triB, triC, aabbCenter, aabbExtents))
                {
                    goto TRUE;
                }

                // Check if the center of the cube is inside the mesh.
                // We check that by raycasting from the point to a random direction,
                // if it intersect and odd number of time, it's inside. Otherwise,
                // it's outside the mesh.
                float3 direction = new(1.0f, 0.0f, 0.0f);
                if (IntersectsRayTriangle(triA, triB, triC, aabbCenter, direction))
                {
                    raycastIntersections++;
                }
            }

            if (raycastIntersections % 2 == 0)
            {
                return;
            }

        TRUE:
            int elementIndex = GetIndex(cords, info.Cells);
            Debug.Assert(elementIndex < voxels.Length, "Index out of range.");
            ((bool*)voxels.GetUnsafePtr())[elementIndex] = true;
        }

       /* [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static long InterlockedOr(long* location1, long value)
        {
            ref long location_ = ref Unsafe.AsRef<long>(location1);
            // TODO: On .NET 5 replace this with Interlocked.Or.
            long current = *location1;
            while (true)
            {
                int newValue = current | value;
                int oldValue = Interlocked.CompareExchange(ref location_, newValue, current);
                if (oldValue == current)
                {
                    return oldValue;
                }
                current = oldValue;
            }
        }*/

        private static bool IntersectsTriangleAABB(float3 triA, float3 triB, float3 triC, float3 aabbCenter, float3 aabbExtents)
        {
            triA -= aabbCenter;
            triB -= aabbCenter;
            triC -= aabbCenter;

            float3 ab = math.normalize(triB - triA);
            float3 bc = math.normalize(triC - triB);
            float3 ca = math.normalize(triA - triC);

            // Cross ab, bc, and ca with (1, 0, 0).
            float3 a00 = new(0.0f, -ab.z, ab.y);
            float3 a01 = new(0.0f, -bc.z, bc.y);
            float3 a02 = new(0.0f, -ca.z, ca.y);

            // Cross ab, bc, and ca with (0, 1, 0).
            float3 a10 = new(ab.z, 0.0f, -ab.x);
            float3 a11 = new(bc.z, 0.0f, -bc.x);
            float3 a12 = new(ca.z, 0.0f, -ca.x);

            // Cross ab, bc, and ca with (0, 0, 1).
            float3 a20 = new(-ab.y, ab.x, 0.0f);
            float3 a21 = new(-bc.y, bc.x, 0.0f);
            float3 a22 = new(-ca.y, ca.x, 0.0f);

            if (
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, a00) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, a01) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, a02) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, a10) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, a11) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, a12) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, a20) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, a21) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, a22) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, new float3(1f, 0f, 0f)) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, new float3(0f, 1f, 0f)) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, new float3(0f, 0f, 1f)) ||
                !IntersectsTriangleAABBAt(triA, triB, triC, aabbExtents, math.cross(ab, bc))
            )
            {
                return false;
            }

            return true;
        }

        private static bool IntersectsTriangleAABBAt(float3 v0, float3 v1, float3 v2, float3 aabbExtents, float3 axis)
        {
            float p0 = math.dot(v0, axis);
            float p1 = math.dot(v1, axis);
            float p2 = math.dot(v2, axis);

            float r = math.csum(aabbExtents * new float3(
                math.abs(math.dot(new float3(1.0f, 0.0f, 0.0f), axis)),
                math.abs(math.dot(new float3(0.0f, 1.0f, 0.0f), axis)),
                math.abs(math.dot(new float3(0.0f, 0.0f, 1.0f), axis))
            ));

            float maxP = math.max(p0, math.max(p1, p2));
            float minP = math.min(p0, math.min(p1, p2));

            return !(math.max(-maxP, minP) > r);
        }

        private static bool IntersectsRayTriangle(float3 v0, float3 v1, float3 v2, float3 origin, float3 direction)
        {
            float3 edge1 = v1 - v0;
            float3 edge2 = v2 - v0;

            float3 h = math.cross(direction, edge2);
            float a = math.dot(edge1, h);

            if (a > -float.Epsilon && a < float.Epsilon)
            {
                // Ray is parallel to the triangle.
                return false;
            }

            float f = 1.0f / a;
            float3 s = origin - v0;
            float u = f * math.dot(s, h);

            if (u < 0.0f || u > 1.0f)
            {
                return false;
            }

            float3 q = math.cross(s, edge1);
            float v = f * math.dot(direction, q);

            if (v < 0.0f || u + v > 1.0f)
            {
                return false;
            }

            // At this stage, we can compute t to find out where the intersection point is on the line.
            float t = f * Vector3.Dot(edge2, q);

            return t > float.Epsilon;
        }
    }

    //[BurstCompile]
    private struct PrepareToVoxelizeJob : IJob
    {
        [WriteOnly]
        private NativeList<bool> voxels;

        private NativeArray<VoxelizationInfo> info;

        public PrepareToVoxelizeJob(NativeList<bool> voxels, NativeArray<VoxelizationInfo> info)
        {
            this.voxels = voxels;
            this.info = info;
        }

        public void Execute()
        {
            VoxelizationInfo info_ = info[0];

            float3 oldSize = info_.Max - info_.Min;
            info_.Cells = (int3)math.ceil(oldSize / info_.VoxelSize) + 1;
            float3 offset = ((float3)info_.Cells * info_.VoxelSize) - oldSize;
            offset *= 0.5f;
            offset += info_.VoxelSize * 1;
            info_.Min -= offset;
            info_.Max += offset;

            info[0] = info_;

            int totalVoxels = info_.Cells.x * info_.Cells.y * info_.Cells.z;
            voxels.Clear();
            voxels.AddReplicate(false, totalVoxels);
        }
    }
}
