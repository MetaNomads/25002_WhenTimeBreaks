using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    [System.Serializable]
    public class RigidbodyTrackingStrategy : ISourceTrackingStrategy
    {
        public float VelocityThreshold
        {
            get
            {
                return _velocityThreshold;
            }
            set
            {
                _velocityThreshold = Mathf.Max(0, value);
            }
        }
        public float AngularVelocityThreshold
        {
            get
            {
                return _angularVelocityThreshold;
            }
            set
            {
                _angularVelocityThreshold = Mathf.Max(0, value);
            }
        }

        [Min(0.01f)]
        [SerializeField]
        private float _velocityThreshold = 0.5f;

        [Min(0.01f)]
        [SerializeField]
        private float _angularVelocityThreshold = 0.3f;

        [SerializeField]
        [HideInInspector]
        private Transform _transform;

        [SerializeField]
        [HideInInspector]
        private Rigidbody _rigidbody;

        private DynamicCombinedObjectPart[] _parts;


        public bool GatherComponents(MeshFusionSource source, out string reason)
        {
            if (!(source is DynamicMeshFusionSource))
            {
                reason = "Source should be DynamicMeshFusionSource";
                return false;
            }

            _transform ??= source.transform;

            if (_transform == null)
            {
                reason = "Transform is missed";
                return false;
            }

            _rigidbody ??= source.GetComponent<Rigidbody>();

            if (_rigidbody == null)
            {
                reason = "Rigidbody is missed";
                return false;
            }

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
            float velocity = UnityAPI.GetRigidbodyVelocity(_rigidbody).magnitude;
            float angularVelocity = _rigidbody.angularVelocity.magnitude;

            changed = velocity > _velocityThreshold || angularVelocity > _angularVelocityThreshold;

            if (!changed)
                return;

            for (int i = 0; i < _parts.Length; i++)
                _parts[i].Move(_transform.localToWorldMatrix);
        }
    }
}
