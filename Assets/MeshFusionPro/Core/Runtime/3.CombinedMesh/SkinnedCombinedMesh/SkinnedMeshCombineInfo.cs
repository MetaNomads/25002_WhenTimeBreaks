using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    public struct SkinnedMeshCombineInfo 
    {
        public readonly MeshCombineInfo MeshCombineInfo;
        public readonly Transform[] Bones;

        public SkinnedMeshCombineInfo(MeshCombineInfo meshCombineInfo, Transform[] bones)
        {
            MeshCombineInfo = meshCombineInfo;
            Bones = bones;
        }

        public SkinnedMeshCombineInfo(SkinnedMeshRenderer renderer, int submeshIndex = 0)
        {
            if (renderer == null)
                throw new NullReferenceException("SkinnedMeshRenderer is null");

            Mesh mesh = renderer.sharedMesh;

            if (mesh == null)
                throw new MissingComponentException($"Mesh not found at SkinnedMeshRenderer: {renderer.name}");

            MeshCombineInfo = new MeshCombineInfo(
                mesh, 
                Matrix4x4.identity, 
                renderer.lightmapScaleOffset, 
                renderer.realtimeLightmapScaleOffset, 
                submeshIndex);

            Bones = renderer.bones;
        }
    }
}
