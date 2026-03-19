using UnityEngine;

public class SetBaseMapColor : MonoBehaviour
{
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Color color = Color.white;

    private void Awake()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
    }

    public void SetColor(Color newColor)
    {
        if (meshRenderer == null)
        {
            Debug.LogError("[SetBaseMapColor] No MeshRenderer assigned.", this);
            return;
        }

        meshRenderer.material.SetColor("_BaseColor", newColor);
    }

    // Applies the serialized color value directly
    [ContextMenu("Apply Color")]
    public void ApplyColor() => SetColor(color);
}
