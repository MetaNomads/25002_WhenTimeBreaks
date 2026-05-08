using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MeshFusionPro
{
    public class SkinnedCombinedObjectMatcher : CombinedObjectMatcher<SkinnedCombinedObject, SkinnedCombineSource>
    {
        private RendererSettings _settings;

        private int _vertexLimit;
        private int _bonesLimit;

        private int _vertexCount;
        private int _bonesCount;

        public SkinnedCombinedObjectMatcher(int vertexLimit, int bonesLimit)
        {
            _vertexLimit = vertexLimit;
            _bonesLimit = bonesLimit;
        }

        public override void StartMatching(SkinnedCombinedObject combinedObject)
        {
            _settings = combinedObject.RendererSettings;

            _vertexCount = combinedObject.VertexCount;
            _bonesCount = combinedObject.BonesCount;
        }

        public override bool CanAddSource(SkinnedCombineSource source)
        {
            if (!_settings.IsEqual(source.RendererSettings))
                return false;

            if ((_vertexCount + source.CombineInfo.MeshCombineInfo.vertexCount) > _vertexLimit)
                return false;

            if ((_bonesCount + source.CombineInfo.Bones.Length) > _bonesLimit)
                return false;

            return true;
        }

        public override void SourceAdded(SkinnedCombineSource source)
        {
            _vertexCount += source.CombineInfo.MeshCombineInfo.vertexCount;
            _bonesCount += source.CombineInfo.Bones.Length;
        }
    }
}
