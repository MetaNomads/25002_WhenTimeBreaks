using UnityEngine;
using UnityEngine.Rendering;
using Oculus.Interaction.Body;

namespace MetaFrame.Recording
{
    public class GizmosCameraFilter : MonoBehaviour
    {
        public enum VisibilityMode
        {
            Disabled,
            PlayerOnly,
            EditorOnly,
            Both
        }

        [SerializeField] private BodyDebugGizmos _gizmos;
        [SerializeField] private VisibilityMode  _mode = VisibilityMode.EditorOnly;

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering   += OnEndCamera;
            ApplyMode();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering   -= OnEndCamera;

            if (_gizmos != null) _gizmos.enabled = false;
        }

        private void OnValidate() => ApplyMode();

        private void ApplyMode()
        {
            if (_gizmos == null) return;
            _gizmos.enabled = _mode != VisibilityMode.Disabled;
        }

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (_gizmos == null || _mode == VisibilityMode.Disabled) return;

            bool isEditor = cam.cameraType == CameraType.SceneView;
            bool isPlayer = cam.cameraType == CameraType.Game;

            _gizmos.enabled = _mode switch
            {
                VisibilityMode.PlayerOnly => isPlayer,
                VisibilityMode.EditorOnly => isEditor,
                VisibilityMode.Both       => isPlayer || isEditor,
                _                         => false
            };
        }

        private void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (_gizmos == null) return;
            _gizmos.enabled = false;
        }
    }
}