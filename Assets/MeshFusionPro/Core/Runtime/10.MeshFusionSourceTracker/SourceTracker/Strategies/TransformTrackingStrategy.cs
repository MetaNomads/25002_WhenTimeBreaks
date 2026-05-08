using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace NGS.MeshFusionPro
{
    [System.Serializable]
    public class TransformTrackingStrategy : ISourceTrackingStrategy
    {
        [SerializeField]
        [HideInInspector]
        private Transform _target;

        private DynamicCombinedObjectPart[] _parts;


        public bool GatherComponents(MeshFusionSource source, out string reason)
        {
            if (!(source is DynamicMeshFusionSource))
            {
                reason = "Source should be DynamicMeshFusionSource";
                return false;
            }

            _target = source.transform;

            reason = "";
            return true;
        }

        public void OnCombineFinished(MeshFusionSource source, IEnumerable<ICombinedObjectPart> parts)
        {
            _parts = parts
                .Select(p => (DynamicCombinedObjectPart) p)
                .ToArray();
        }

        public void Track(out bool changed)
        {
            changed = _target.hasChanged;

            if (!changed)
                return;

            for (int i = 0; i < _parts.Length; i++)
                _parts[i].Move(_target.localToWorldMatrix);

            _target.hasChanged = false;
        }
    }
}
