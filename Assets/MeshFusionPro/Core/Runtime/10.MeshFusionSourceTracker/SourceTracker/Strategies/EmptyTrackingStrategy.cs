using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    [System.Serializable]
    public class EmptyTrackingStrategy : ISourceTrackingStrategy
    {
        public bool GatherComponents(MeshFusionSource source, out string reason)
        {
            reason = "";
            return true;
        }

        public void OnCombineFinished(MeshFusionSource source, IEnumerable<ICombinedObjectPart> parts)
        {
            
        }

        public void Track(out bool changed)
        {
            changed = false;
        }
    }
}
