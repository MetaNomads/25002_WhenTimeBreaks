using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.MeshFusionPro
{
    public class MeshSeparatorSimple
    {
        private const int MAX_UV_CHANNELS = 4;

        private static Dictionary<Mesh, Mesh[]> _meshToSubmeshes;

        private static List<int> _srcTriangles;
        private static List<Vector3> _srcVertices;
        private static List<Vector3> _srcNormals;
        private static List<Vector4> _srcTangents;
        private static List<Vector2> _srcUV;


        static MeshSeparatorSimple()
        {
            _meshToSubmeshes = new Dictionary<Mesh, Mesh[]>();

            _srcTriangles = new List<int>();
            _srcVertices = new List<Vector3>();
            _srcNormals = new List<Vector3>();
            _srcTangents = new List<Vector4>();
            _srcUV = new List<Vector2>();
        }

        public static void ClearCache()
        {
            _meshToSubmeshes.Clear();

            ClearMeshData();
        }


        public Mesh GetSubmesh(Mesh source, int submesh)
        {
            Mesh[] submeshes;

            if (!_meshToSubmeshes.TryGetValue(source, out submeshes))
            {
                submeshes = Separate(source);

                _meshToSubmeshes.Add(source, submeshes);
            }

            return submeshes[submesh];
        }


        private Mesh[] Separate(Mesh mesh)
        {
            int subMeshCount = mesh.subMeshCount;

            Mesh[] separated = new Mesh[subMeshCount];

            CollectMeshData(mesh);

            for (int i = 0; i < subMeshCount; i++)
                separated[i] = CreateFromSubmesh(mesh, i);

            ClearMeshData();

            return separated;
        }

        private void CollectMeshData(Mesh mesh)
        {
            mesh.GetVertices(_srcVertices);
            mesh.GetNormals(_srcNormals);
            mesh.GetTangents(_srcTangents);
        }

        private Mesh CreateFromSubmesh(Mesh mesh, int submesh)
        {
            SubMeshDescriptor desc = mesh.GetSubMesh(submesh);

            Mesh result = new Mesh();

            int trianglesCount = desc.indexCount;
            int vertexStart = desc.firstVertex;
            int vertexCount = desc.vertexCount;
            
            mesh.GetIndices(_srcTriangles, submesh);

            for (int i = 0; i < trianglesCount; i++)
            {
                _srcTriangles[i] -= vertexStart;
            }

            result.SetVertices(_srcVertices, vertexStart, vertexCount);

            if (_srcNormals.Count > 0)
                result.SetNormals(_srcNormals, vertexStart, vertexCount);

            if (_srcTangents.Count > 0)
                result.SetTangents(_srcTangents, vertexStart, vertexCount);

            result.SetTriangles(_srcTriangles, 0, false);
            result.bounds = desc.bounds;

            for (int i = 0; i < MAX_UV_CHANNELS; i++)
            {
                mesh.GetUVs(i, _srcUV);

                if (_srcUV.Count == 0)
                    continue;

                result.SetUVs(i, _srcUV, vertexStart, vertexCount);
            }

            return result;
        }

        private static void ClearMeshData()
        {
            _srcTriangles?.Clear();
            _srcVertices?.Clear();
            _srcNormals?.Clear();
            _srcTangents?.Clear();
            _srcUV?.Clear();
        }
    }
}
