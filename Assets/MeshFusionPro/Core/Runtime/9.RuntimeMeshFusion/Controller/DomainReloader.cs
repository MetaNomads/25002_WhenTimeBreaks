using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    public partial class RuntimeMeshFusion : MonoBehaviour
    {
        private partial class DomainReloader
        {
            public static void Reload()
            {
                _Instances = null;

                MeshSeparatorSimple.ClearCache();
            }
        }
    }
}