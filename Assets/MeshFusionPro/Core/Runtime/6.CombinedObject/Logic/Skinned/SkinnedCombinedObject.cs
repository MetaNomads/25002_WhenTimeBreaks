using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    public class SkinnedCombinedObject : MonoBehaviour, ICombinedObject<SkinnedCombinedObjectPart, SkinnedCombineSource>
    {
        IReadOnlyList<ICombinedObjectPart> ICombinedObject.Parts
        {
            get
            {
                return _parts;
            }
        }
        public IReadOnlyList<SkinnedCombinedObjectPart> Parts
        {
            get
            {
                return _parts;
            }
        }

        public SkinnedMeshRenderer Renderer { get; private set; }
        public RendererSettings RendererSettings { get; private set; }

        public Bounds Bounds
        {
            get
            {
                return Renderer.bounds;
            }
        }
        public Bounds LocalBounds
        {
            get
            {
                return Renderer.localBounds;
            }
        }

        public int VertexCount
        {
            get
            {
                return _combinedMesh.Mesh.vertexCount;
            }
        }
        public int BonesCount
        {
            get
            {
                return _combinedMesh.Bones.Count;
            }
        }

        private SkinnedCombinedMesh _combinedMesh;
        private List<SkinnedCombinedObjectPart> _parts;

        private HashSet<SkinnedCombinedObjectPart> _destroyPartsQueue;
        private HashSet<CombinedMeshPart> _destroyMeshPartsQueue;
        private bool _recalculateBounds;


        public static SkinnedCombinedObject Create(RendererSettings settings)
        {
            return new GameObject("Skinned Combined Object")
                .AddComponent<SkinnedCombinedObject>()
                .Construct(settings);
        }

        private SkinnedCombinedObject Construct(RendererSettings settings)
        {
            _combinedMesh = new SkinnedCombinedMesh();
            _parts = new List<SkinnedCombinedObjectPart>();

            _destroyPartsQueue = new HashSet<SkinnedCombinedObjectPart>();
            _destroyMeshPartsQueue = new HashSet<CombinedMeshPart>();

            Renderer = CreateRenderer(settings);
            RendererSettings = settings;

            Renderer.sharedMesh = _combinedMesh.Mesh;
            Renderer.bones = _combinedMesh.Bones.ToArray();

            return this;
        }


        private void Update()
        {
            if (_destroyPartsQueue.Count > 0)
            {
                _combinedMesh.Cut(_destroyMeshPartsQueue.ToArray());

                foreach (var destroyPart in _destroyPartsQueue)
                    _parts.Remove(destroyPart);

                _destroyPartsQueue.Clear();
                _destroyMeshPartsQueue.Clear();
            }

            if (_recalculateBounds)
                RecalculateBoundsImmediate();

            enabled = false;
        }

        private void OnDestroy()
        {
            _combinedMesh.Dispose();
        }


        public void Combine(IEnumerable<ICombineSource> sources)
        {
            Combine(sources.Select(s => (SkinnedCombineSource)sources));
        }

        public void Combine(IEnumerable<SkinnedCombineSource> sources)
        {
            if (_parts.Count == 0)
                transform.position = GetAveragePosition(sources);

            int count = sources.Count();

            SkinnedMeshCombineInfo[] combineInfos = new SkinnedMeshCombineInfo[count];

            int idx = 0;
            foreach (var source in sources)
            {
                combineInfos[idx] = source.CombineInfo;
                idx++;
            }

            try
            {
                CombinedMeshPart[] meshParts = _combinedMesh.Combine(combineInfos);

                Renderer.bones = _combinedMesh.Bones.ToArray();

                idx = 0;
                foreach (var source in sources)
                {
                    SkinnedCombinedObjectPart part = new SkinnedCombinedObjectPart(this, meshParts[idx]);

                    _parts.Add(part);

                    source.Combined(this, part);

                    idx++;
                }
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message + ex.StackTrace;

                foreach (var source in sources)
                {
                    source.CombineError(this, errorMessage);
                    source.CombineFailed(this);
                }
            }
        }

        public void RecalculateBounds()
        {
            _recalculateBounds = true;

            enabled = true;
        }

        public void RecalculateBoundsImmediate()
        {
            List<Transform> bones = _combinedMesh.Bones;

            if (bones.Count == 0)
                return;

            int start = 0;
            Bounds bounds = default;

            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != null)
                {
                    start = i + 1;
                    bounds = new Bounds(Renderer.transform.InverseTransformPoint(bones[i].position), Vector3.zero);
                    break;
                }
            }

            if (start == bones.Count)
                return;

            for (int i = start; i < bones.Count; i++)
            {
                if (bones[i] == null)
                    continue;

                bounds.Encapsulate(Renderer.transform.InverseTransformPoint(bones[i].position));
            }

            Renderer.localBounds = bounds;
        }

        public void Destroy(SkinnedCombinedObjectPart destroyPart)
        {
            if (_parts.Contains(destroyPart))
            {
                _destroyPartsQueue.Add(destroyPart);
                _destroyMeshPartsQueue.Add(destroyPart.MeshPart);

                enabled = true;
            }
        }


        private SkinnedMeshRenderer CreateRenderer(RendererSettings settings)
        {
            SkinnedMeshRenderer renderer = gameObject.AddComponent<SkinnedMeshRenderer>();

            settings.ApplyTo(renderer);

            return renderer;
        }

        private Vector3 GetAveragePosition(IEnumerable<ICombineSource> sources)
        {
            Vector3 average = Vector3.zero;

            int count = 0;

            foreach (var source in sources)
            {
                average += source.Position;
                count++;
            }

            return (average / count);
        }
    }
}
