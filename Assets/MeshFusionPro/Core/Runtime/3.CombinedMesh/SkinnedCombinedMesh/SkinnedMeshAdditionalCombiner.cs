using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;


namespace NGS.MeshFusionPro
{
    public class SkinnedMeshAdditionalCombiner
    {
        public List<Transform> Bones
        {
            get
            {
                return _bones;
            }
        }

        private List<Transform> _bones;
        private List<Matrix4x4> _bindposes;
        private List<BoneWeight> _boneWeights;

        private Dictionary<BoneInfo, int> _boneToIndex;

        private Dictionary<int, int> _tempBonesMap;
        private List<BoneWeight> _tempBoneWeights;
        private List<Matrix4x4> _tempBindposes;


        public SkinnedMeshAdditionalCombiner()
        {
            _bones = new List<Transform>();
            _bindposes = new List<Matrix4x4>();
            _boneWeights = new List<BoneWeight>();

            _boneToIndex = new Dictionary<BoneInfo, int>();

            _tempBonesMap = new Dictionary<int, int>();
            _tempBindposes = new List<Matrix4x4>();
            _tempBoneWeights = new List<BoneWeight>();
        }

        public void Combine(IList<SkinnedMeshCombineInfo> combineInfos)
        {
            for (int i = 0; i < combineInfos.Count; i++)
            {
                _tempBonesMap.Clear();
                _tempBindposes.Clear();
                _tempBoneWeights.Clear();

                SkinnedMeshCombineInfo combineInfo = combineInfos[i];

                Mesh sourceMesh = combineInfo.MeshCombineInfo.mesh;
                Transform[] sourceBones = combineInfo.Bones;

                SubMeshDescriptor submesh = sourceMesh.GetSubMesh(combineInfo.MeshCombineInfo.submeshIndex);

                sourceMesh.GetBindposes(_tempBindposes);
                sourceMesh.GetBoneWeights(_tempBoneWeights);

                for (int c = 0; c < sourceBones.Length; c++)
                {
                    BoneInfo boneInfo = new BoneInfo(sourceBones[c], _tempBindposes[c]);

                    int boneIndex = -1;
                    
                    if (!_boneToIndex.TryGetValue(boneInfo, out boneIndex))
                    {
                        boneIndex = _bones.Count;

                        _bones.Add(boneInfo.bone);
                        _bindposes.Add(boneInfo.bindpose);

                        _boneToIndex.Add(boneInfo, boneIndex);
                    }

                    _tempBonesMap.Add(c, boneIndex);
                }

                int start = submesh.firstVertex;
                int end = start + submesh.vertexCount;

                for (int c = start; c < end; c++)
                {
                    BoneWeight boneWeight = _tempBoneWeights[c];

                    boneWeight.boneIndex0 = _tempBonesMap[boneWeight.boneIndex0];
                    boneWeight.boneIndex1 = _tempBonesMap[boneWeight.boneIndex1];
                    boneWeight.boneIndex2 = _tempBonesMap[boneWeight.boneIndex2];
                    boneWeight.boneIndex3 = _tempBonesMap[boneWeight.boneIndex3];

                    _boneWeights.Add(boneWeight);
                }
            }
        }

        public void Cut(IList<CombinedMeshPart> parts)
        {
            foreach (var removePart in parts.OrderByDescending(p => p.VertexStart))
            {
                _boneWeights.RemoveRange(removePart.VertexStart, removePart.VertexCount);
            }
        }

        public void Apply(Mesh mesh)
        {
            mesh.boneWeights = _boneWeights.ToArray();
            mesh.bindposes = _bindposes.ToArray();
        }


        private struct BoneInfo
        {
            public Transform bone;
            public Matrix4x4 bindpose;

            public BoneInfo(Transform bone, Matrix4x4 bindpose)
            {
                this.bone = bone;
                this.bindpose = bindpose;
            }
        }
    }
}
