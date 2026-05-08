using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    public class SkinnedCombinedMesh : IDisposable
    {
        public Mesh Mesh 
        {
            get
            {
                return _combinedMesh.Mesh;
            }
        }
        public List<Transform> Bones
        {
            get
            {
                return _additionalCombiner.Bones;
            }
        }

        private CombinedMesh _combinedMesh;
        private SkinnedMeshAdditionalCombiner _additionalCombiner;


        public SkinnedCombinedMesh()
        {
            _combinedMesh = new CombinedMesh(new MeshCombinerSimpleSTD(), new MeshCutterSimpleSTD());
            _additionalCombiner = new SkinnedMeshAdditionalCombiner();
        }

        public CombinedMeshPart[] Combine(IList<SkinnedMeshCombineInfo> skinnedMeshCombineInfos)
        {
            MeshCombineInfo[] meshCombineInfos = new MeshCombineInfo[skinnedMeshCombineInfos.Count];

            for (int i = 0; i < skinnedMeshCombineInfos.Count; i++)
                meshCombineInfos[i] = skinnedMeshCombineInfos[i].MeshCombineInfo;

            CombinedMeshPart[] combinedParts = _combinedMesh.Combine(meshCombineInfos);

            _additionalCombiner.Combine(skinnedMeshCombineInfos);
            _additionalCombiner.Apply(Mesh);

            return combinedParts;
        }

        public void Cut(IList<CombinedMeshPart> parts)
        {
            _additionalCombiner.Cut(parts);

            _combinedMesh.Cut(parts);

            _additionalCombiner.Apply(Mesh);
        }

        public void Dispose()
        {
            _combinedMesh.Dispose();
        }
    }
}
