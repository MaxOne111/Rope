using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RopeMeshRenderer : MonoBehaviour
{
    [SerializeField] private GrapplingHookVerlet rope;
    
    [SerializeField] private float ropeWidth = 0.1f;

    private Mesh _mesh;
    
    private NativeArray<Vector3> _verticesNative;
    private NativeArray<Vector2> _uvsNative;
    private NativeArray<int> _trianglesNative;

    private JobHandle _meshJobHandle;

    private List<Vector3> _verticesList = new List<Vector3>();
    private List<Vector2> _uvsList = new List<Vector2>();
    private List<int> _trianglesList = new List<int>();

    private void Start()
    {
        _mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = _mesh;
    }

    private void LateUpdate()
    {
        if (rope == null || rope.Segments == null || rope.Segments.Count < 2)
        {
            _mesh.Clear();
            return;
        }

        if (!rope.NativeArraysInitialized)
        {
            _mesh.Clear();
            return;
        }

        _meshJobHandle.Complete();

        UpdateMeshWithJob();
    }

    private void UpdateMeshWithJob()
    {
        var segments = rope.NativeSegments;
        int segmentCount = segments.Length;
        
        const int verticesPerSegment = 4; 
        const int indicesPerTriangle = 3;
        const int trianglesPerSegment = 2;
        const int indicesPerSegment = indicesPerTriangle * trianglesPerSegment;
        
        int quadCount = segmentCount - 1;
        
        int vertCount = quadCount * verticesPerSegment;
        int triCount = quadCount * indicesPerSegment;
        
        if (!_verticesNative.IsCreated || _verticesNative.Length != vertCount)
        {
            DisposeNativeArrays();

            _verticesNative = new NativeArray<Vector3>(vertCount, Allocator.Persistent);
            _uvsNative = new NativeArray<Vector2>(vertCount, Allocator.Persistent);
            _trianglesNative = new NativeArray<int>(triCount, Allocator.Persistent);
        }

        float interpolationFactor = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        interpolationFactor = Mathf.Clamp01(interpolationFactor);

        Vector3 parentPos = transform.parent ? transform.parent.position : Vector3.zero;

        var job = new RopeMeshJob()
        {
            segments = rope.NativeSegments,
            interpolationFactor = interpolationFactor,
            ropeWidth = ropeWidth,
            parentPosition = parentPos,
            vertices = _verticesNative,
            uvs = _uvsNative,
            triangles = _trianglesNative,
        };

        JobHandle handle = job.Schedule();
        handle.Complete();
        
        _verticesList.Clear();
        _uvsList.Clear();
        _trianglesList.Clear();

        for (int i = 0; i < _verticesNative.Length; i++)
        {
            _verticesList.Add(_verticesNative[i]);
            _uvsList.Add(_uvsNative[i]);
        }

        for (int i = 0; i < _trianglesNative.Length; i++)
        {
            _trianglesList.Add(_trianglesNative[i]);
        }

        _mesh.Clear();
        _mesh.SetVertices(_verticesList);
        _mesh.SetUVs(0, _uvsList);
        _mesh.SetTriangles(_trianglesList, 0);
        _mesh.RecalculateNormals();
    }

    private void DisposeNativeArrays()
    {
        if (_verticesNative.IsCreated) _verticesNative.Dispose();
        if (_uvsNative.IsCreated) _uvsNative.Dispose();
        if (_trianglesNative.IsCreated) _trianglesNative.Dispose();
    }

    private void OnDestroy()
    {
        DisposeNativeArrays();
    }

    [BurstCompile]
    private struct RopeMeshJob : IJob
    {
        [ReadOnly] public NativeArray<GrapplingHookVerlet.VerletSegment> segments;
        public float interpolationFactor;
        public float ropeWidth;
        public Vector3 parentPosition;

        public NativeArray<Vector3> vertices;
        public NativeArray<Vector2> uvs;
        public NativeArray<int> triangles;

        public void Execute()
        {
            int segmentCount = segments.Length;

            const int verticesPerSegment = 4;
            const int indicesPerTriangle = 3;
            const int trianglesPerSegment = 2;
            const int indicesPerSegment = indicesPerTriangle * trianglesPerSegment;

            int quadCount = segmentCount - 1;

            for (int i = 0; i < quadCount; i++)
            {
                int vertIndex = i * verticesPerSegment;
                int triIndex = i * indicesPerSegment;

                Vector3 posA = Vector3.Lerp(segments[i].previousPosition, segments[i].position, interpolationFactor);
                Vector3 posB = Vector3.Lerp(segments[i + 1].previousPosition, segments[i + 1].position, interpolationFactor);

                Vector3 forward = (posB - posA).normalized;
                Vector3 side = Vector3.Cross(forward, Vector3.up).normalized * ropeWidth * 0.5f;

                Vector3 localPosA = posA - parentPosition;
                Vector3 localPosB = posB - parentPosition;

                vertices[vertIndex + 0] = localPosA + side;
                vertices[vertIndex + 1] = localPosA - side;
                vertices[vertIndex + 2] = localPosB + side;
                vertices[vertIndex + 3] = localPosB - side;

                float v0 = i / (float)quadCount;
                float v1 = (i + 1) / (float)quadCount;

                uvs[vertIndex + 0] = new Vector2(0f, v0);
                uvs[vertIndex + 1] = new Vector2(1f, v0);
                uvs[vertIndex + 2] = new Vector2(0f, v1);
                uvs[vertIndex + 3] = new Vector2(1f, v1);
                
                const int leftA = 0;
                const int rightA = 1;
                const int leftB = 2;
                const int rightB = 3;

                triangles[triIndex + 0] = vertIndex + leftA;
                triangles[triIndex + 1] = vertIndex + leftB;
                triangles[triIndex + 2] = vertIndex + rightA;

                triangles[triIndex + 3] = vertIndex + rightA;
                triangles[triIndex + 4] = vertIndex + leftB;
                triangles[triIndex + 5] = vertIndex + rightB;
            }
        }
    }

}
