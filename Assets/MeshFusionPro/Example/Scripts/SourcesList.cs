using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MeshFusionPro.Example
{
    public class SourcesList : MonoBehaviour
    {
        public static bool UpdatedDirty { get; set; }
        public static IReadOnlyCollection<MeshFusionSource> Sources
        {
            get
            {
                return _sources;
            }
        }
        public static IReadOnlyCollection<MeshRenderer> CombinedObjects
        {
            get
            {
                return _combinedObjects;
            }
        }

        private static HashSet<MeshRenderer> _combinedObjects;
        private static HashSet<MeshFusionSource> _sources;


        static SourcesList()
        {
            _combinedObjects = new HashSet<MeshRenderer>();
            _sources = new HashSet<MeshFusionSource>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDomain()
        {
            UpdatedDirty = false;

            if (_combinedObjects != null)
                _combinedObjects.Clear();

            if (_sources != null)
                _sources.Clear();
        }


        private void Awake()
        {
            MeshFusionSource source = GetComponent<MeshFusionSource>();

            if (source == null)
                throw new MissingComponentException();

            source.onCombineFinished += OnSourceCombined;

            _sources.Add(source);

            UpdatedDirty = true;
        }

        private void OnSourceCombined(MeshFusionSource source, IEnumerable<ICombinedObjectPart> parts)
        {
            foreach (var part in parts)
            {
                if (part is CombinedLODGroupPart)
                {
                    MeshRenderer[] renderers = ((MonoBehaviour)part.Root).GetComponentsInChildren<MeshRenderer>();

                    for (int i = 0; i < renderers.Length; i++)
                        _combinedObjects.Add(renderers[i]);
                }
                else
                {
                    _combinedObjects.Add(((MonoBehaviour)part.Root).GetComponent<MeshRenderer>());
                }
            }

            UpdatedDirty = true;
        }
    }
}
