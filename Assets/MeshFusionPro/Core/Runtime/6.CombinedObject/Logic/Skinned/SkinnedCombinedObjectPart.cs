using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    public class SkinnedCombinedObjectPart : ICombinedObjectPart<SkinnedCombinedObject>
    {
        ICombinedObject ICombinedObjectPart.Root
        {
            get
            {
                return Root;
            }
        }
        public SkinnedCombinedObject Root { get; private set; }
        public CombinedMeshPart MeshPart { get; private set; }

        public Bounds Bounds
        {
            get
            {
                throw new NotSupportedException();
            }
        }
        public Bounds LocalBounds
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        private bool _destroyed;


        public SkinnedCombinedObjectPart(SkinnedCombinedObject root, CombinedMeshPart meshPart)
        {
            Root = root;
            MeshPart = meshPart;
        }

        public void Destroy()
        {
            if (_destroyed)
            {
                Debug.Log("SkinnedCombinedObjectPart already destroyed!");
                return;
            }

            Root.Destroy(this);

            _destroyed = true;
        }
    }
}
