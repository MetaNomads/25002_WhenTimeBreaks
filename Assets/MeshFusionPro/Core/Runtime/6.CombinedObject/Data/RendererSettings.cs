using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.MeshFusionPro
{
    public struct RendererSettings
    {
        public string tag;
        public int layer;
        public uint renderingLayerMask;
        public Material material;
        public ShadowCastingMode shadowMode;
        public bool receiveShadows;
        public int lightmapIndex;
        public MotionVectorGenerationMode motionVectorGenerationMode;
        public bool updateWhenOffScreen;


        public RendererSettings(Renderer renderer, int materialIndex = 0)
        {
            material = renderer.GetSharedMaterialWithoutAlloc(materialIndex);
            shadowMode = renderer.shadowCastingMode;
            receiveShadows = renderer.receiveShadows;
            lightmapIndex = renderer.lightmapIndex;
            tag = renderer.tag;
            layer = renderer.gameObject.layer;
            renderingLayerMask = renderer.renderingLayerMask;
            motionVectorGenerationMode = renderer.motionVectorGenerationMode;
            updateWhenOffScreen = (renderer is SkinnedMeshRenderer skinned) && skinned.updateWhenOffscreen;
        }

        public bool IsEqual(RendererSettings settings)
        {
            return 
                tag == settings.tag &&
                layer == settings.layer &&
                renderingLayerMask == settings.renderingLayerMask &&
                material == settings.material &&
                shadowMode == settings.shadowMode &&
                receiveShadows == settings.receiveShadows &&
                lightmapIndex == settings.lightmapIndex &&
                motionVectorGenerationMode == settings.motionVectorGenerationMode &&
                updateWhenOffScreen == settings.updateWhenOffScreen;
        }

        public void ApplyTo(Renderer renderer)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadowMode;
            renderer.receiveShadows = receiveShadows;
            renderer.lightmapIndex = lightmapIndex;
            renderer.tag = tag;
            renderer.gameObject.layer = layer;
            renderer.renderingLayerMask = renderingLayerMask;
            renderer.motionVectorGenerationMode = motionVectorGenerationMode;

            if (renderer is SkinnedMeshRenderer skinned)
                skinned.updateWhenOffscreen = updateWhenOffScreen;
        }
    }
}
