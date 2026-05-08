using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    public enum TrackingTarget { None, Transform, Rigidbody, SkinnedMesh }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFusionSource))]
    public class SourceTracker : MonoBehaviour
    {
        public TrackingTarget TrackingTarget
        {
            get
            {
                return _trackingTarget;
            }
            set
            {
                if (_trackingTarget == value)
                    return;

                OnChangeTrackingTarget(value);
            }
        }
        public ISourceTrackingStrategy TrackingStrategy
        {
            get
            {
                return _trackingStrategy;
            }
        }

        [field: SerializeField]
        public bool DisableWhenIdle { get; set; } = true;

        [field: SerializeField, Min(0.01f)]
        public float MaxIdleTime { get; set; } = 5f;

        [field: SerializeField]
        public bool WakeUpWhenCollision { get; set; } = true;

        [field: SerializeField]
        public bool TrackingDestroy { get; set; } = true;

        [SerializeField]
        private MeshFusionSource _source;

        [SerializeField]
        private TrackingTarget _trackingTarget;

        [SerializeReference]
        private ISourceTrackingStrategy _trackingStrategy;

        private IEnumerable<ICombinedObjectPart> _parts;
        private float _idleTime;
        private Action _updateFunc;


        public void WakeUp()
        {
            enabled = true;

            _idleTime = 0f;
        }


        private void Reset()
        {
            _source = GetComponent<MeshFusionSource>();

            if (_source is DynamicMeshFusionSource)
            {
                if (TryGetComponent<Rigidbody>(out _))
                    TrackingTarget = TrackingTarget.Rigidbody;
                else
                    TrackingTarget = TrackingTarget.Transform;
            }
            else if (_source is SkinnedMeshFusionSource)
            {
                TrackingTarget = TrackingTarget.SkinnedMesh;
            }
            else
            {
                TrackingTarget = TrackingTarget.None;
            }
        }

        private void Awake()
        {
            _source ??= GetComponent<MeshFusionSource>();

            _source.onCombineFinished += OnCombineFinished;

            enabled = false;
        }

        private void Update()
        {
            try
            {
                _updateFunc?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.Log($"Error occured while tracking source: {ex.Message}\n{ex.StackTrace}");

                enabled = false;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (WakeUpWhenCollision && _parts != null)
                WakeUp();
        }

        private void OnDestroy()
        {
            if (!gameObject.scene.isLoaded)
                return;

            if (!TrackingDestroy || _parts == null)
                return;

            foreach (var part in _parts)
            {
                if (part != null && part.Root != null)
                    part.Destroy();
            }
        }

        
        private void OnChangeTrackingTarget(TrackingTarget value)
        {
            _trackingTarget = value;

            CreateTrackingStrategy();

            if (!_trackingStrategy.GatherComponents(_source, out string reason))
            {
                Debug.Log($"Unable to create TrackingStrategy. Reason: {reason}");

                OnChangeTrackingTarget(TrackingTarget.None);
            }
        }

        private void OnCombineFinished(MeshFusionSource source, IEnumerable<ICombinedObjectPart> parts)
        {
            _parts = parts;

            _source.onCombineFinished -= OnCombineFinished;

            _trackingStrategy?.OnCombineFinished(source, _parts);

            if (TrackingTarget != TrackingTarget.None)
            {
                _updateFunc = DisableWhenIdle ? UpdateTrackerAndCheckIdle : UpdateTracker;

                enabled = true;
            }
        }


        private void CreateTrackingStrategy()
        {
            if (_trackingTarget == TrackingTarget.None)
            {
                _trackingStrategy = new EmptyTrackingStrategy();
            }
            else if (_trackingTarget == TrackingTarget.Transform)
            {
                _trackingStrategy = new TransformTrackingStrategy();
            }
            else if (_trackingTarget == TrackingTarget.Rigidbody)
            {
                _trackingStrategy = new RigidbodyTrackingStrategy();
            }
            else if (_trackingTarget == TrackingTarget.SkinnedMesh)
            {
                _trackingStrategy = new SkinnedMeshTrackingStrategy();
            }
        }

        private void UpdateTracker()
        {
            _trackingStrategy.Track(out _);
        }

        private void UpdateTrackerAndCheckIdle()
        {
            _trackingStrategy.Track(out bool changed);
                
            if (changed)
            {
                _idleTime = 0f;
            }
            else
            {
                _idleTime += Time.deltaTime;

                if (_idleTime > MaxIdleTime)
                {
                    enabled = false;

                    _idleTime = 0f;
                }
            }
        }
    }
}
