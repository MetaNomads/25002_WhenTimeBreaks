using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    public class SkinnedMeshFusionSource : MeshFusionSource
    {
        [SerializeField, HideInInspector]
        private SkinnedMeshRenderer _renderer;

        [SerializeField, HideInInspector]
        private Mesh _mesh;

        private List<SkinnedCombineSource> _sources;
        private List<SkinnedCombinedObjectPart> _parts;


        public override bool TryGetBounds(ref Bounds bounds)
        {
            if (_renderer == null)
                return false;

            bounds = _renderer.bounds;
            return true;
        }


        protected override bool CheckCompatibilityAndGetComponents(out string incompatibilityReason)
        {
            if (_renderer == null)
                _renderer = GetComponent<SkinnedMeshRenderer>();

            if (_renderer == null)
            {
                incompatibilityReason = "SkinnedMeshRenderer not found";
                return false;
            }

            if (_renderer.isPartOfStaticBatch)
            {
                incompatibilityReason = "SkinnedMeshRenderer is PartOfStaticBatching" +
                    "\nDisable Static Batching";
                return false;
            }
            
            _mesh = _renderer.sharedMesh;

            if (_mesh == null)
            {
                incompatibilityReason = "Mesh not found";
                return false;
            }

            if (!_mesh.isReadable)
            {
                incompatibilityReason = "Mesh is not readable. Enable Read/Write in import settings";
                return false;
            }

            if (_mesh.subMeshCount != _renderer.GetMaterialsCount())
            {
                incompatibilityReason = "Submesh count and Materials count isn't equal";
                return false;
            }

            incompatibilityReason = "";
            return true;
        }

        protected override void ToggleComponents(bool enabled)
        {
            _renderer.enabled = enabled;
        }

        protected override void OnSourceCombinedInternal(ICombinedObject root, ICombinedObjectPart part)
        {
            _parts.Add((SkinnedCombinedObjectPart)part);
        }


        protected override void CreateSources()
        {
            int subMeshCount = _mesh.subMeshCount;

            if (_sources == null)
                _sources = new List<SkinnedCombineSource>(subMeshCount);

            if (_parts == null)
                _parts = new List<SkinnedCombinedObjectPart>(subMeshCount);

            for (int i = 0; i < subMeshCount; i++)
            {
                _sources.Add(new SkinnedCombineSource(_renderer, i));
            }
        }

        protected override IEnumerable<ICombineSource> GetCombineSources()
        {
            if (_sources == null)
                yield break;

            for (int i = 0; i < _sources.Count; i++)
                yield return _sources[i];
        }

        protected override IEnumerable<ICombinedObjectPart> GetCombinedParts()
        {
            if (_parts == null)
                yield break;

            for (int i = 0; i < _parts.Count; i++)
                yield return _parts[i];
        }


        protected override void ClearSources()
        {
            _sources?.Clear();
        }

        protected override void ClearParts()
        {
            _parts?.Clear();
        }
    }
}
