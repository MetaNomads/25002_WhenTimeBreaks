using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    public class SkinnedObjectsCombiner : ObjectsCombiner<SkinnedCombinedObject, SkinnedCombineSource>
    {
        private SkinnedCombinedObjectMatcher _matcher;
        private int _vertexLimit;
        private int _bonesLimit;

        public SkinnedObjectsCombiner(int vertexLimit, int bonesLimit)
        {
            _matcher = new SkinnedCombinedObjectMatcher(vertexLimit, bonesLimit);
            _vertexLimit = vertexLimit;
            _bonesLimit = bonesLimit;
        }

        public override void AddSource(SkinnedCombineSource source)
        {
            if (source.CombineInfo.MeshCombineInfo.vertexCount >= _vertexLimit)
                return;

            if (source.CombineInfo.Bones.Length >= _bonesLimit)
                return;

            base.AddSource(source);
        }


        protected override SkinnedCombinedObject CreateCombinedObject(SkinnedCombineSource source)
        {
            return SkinnedCombinedObject.Create(source.RendererSettings);
        }

        protected override void CombineSources(SkinnedCombinedObject root, IList<SkinnedCombineSource> sources)
        {
            root.Combine(sources);
        }

        protected override CombinedObjectMatcher<SkinnedCombinedObject, SkinnedCombineSource> GetMatcher()
        {
            return _matcher;
        }
    }
}
