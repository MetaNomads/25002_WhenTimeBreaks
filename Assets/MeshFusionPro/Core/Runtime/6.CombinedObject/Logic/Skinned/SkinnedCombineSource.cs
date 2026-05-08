using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    public class SkinnedCombineSource : ICombineSource<SkinnedCombinedObject, SkinnedCombinedObjectPart>
    {
        public Vector3 Position { get; private set; }
        public Bounds Bounds { get; private set; }
        public SkinnedMeshCombineInfo CombineInfo { get; private set; }
        public RendererSettings RendererSettings { get; private set; }

        public event Action<ICombinedObject, ICombinedObjectPart> onCombined;
        public event Action<ICombinedObject, string> onCombineError;
        public event Action<ICombinedObject> onCombineFailed;

        public event Action<SkinnedCombinedObject, SkinnedCombinedObjectPart> onCombinedTyped;
        public event Action<SkinnedCombinedObject, string> onCombineErrorTyped;
        public event Action<SkinnedCombinedObject> onCombineFailedTyped;


        public SkinnedCombineSource(SkinnedMeshRenderer renderer, int submeshIndex)
        {
            if (renderer == null)
                throw new ArgumentNullException("SkinnedMeshRenderer is null");

            Mesh mesh = renderer.sharedMesh;

            if (mesh == null)
                throw new ArgumentNullException("Mesh is null");

            if (submeshIndex >= mesh.subMeshCount)
                throw new ArgumentException("'submeshIndex' is greater then submeshes count");

            if (submeshIndex >= renderer.GetMaterialsCount())
                throw new ArgumentException("'submeshIndex' is greater then materials count");

            SkinnedMeshCombineInfo combineInfo = new SkinnedMeshCombineInfo(renderer, submeshIndex);
            RendererSettings rendererSettings = new RendererSettings(renderer, submeshIndex);

            CombineInfo = combineInfo;
            RendererSettings = rendererSettings;
            Position = renderer.transform.position;
            Bounds = renderer.bounds;
        }


        public void Combined(SkinnedCombinedObject root, SkinnedCombinedObjectPart part)
        {
            onCombined?.Invoke(root, part);
            onCombinedTyped?.Invoke(root, part);
        }

        public void CombineError(SkinnedCombinedObject root, string errorMessage)
        {
            if (onCombineError == null && onCombinedTyped == null)
            {
                Debug.Log("Error during combine " + root.name + ", reason :" + errorMessage);
                return;
            }

            onCombineError?.Invoke(root, errorMessage);
            onCombineErrorTyped?.Invoke(root, errorMessage);
        }

        public void CombineFailed(SkinnedCombinedObject root)
        {
            onCombineFailed?.Invoke(root);
            onCombineFailedTyped?.Invoke(root);
        }
    }
}
