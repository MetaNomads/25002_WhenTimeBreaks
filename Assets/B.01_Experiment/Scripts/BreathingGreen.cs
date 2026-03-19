using System.Collections;
using UnityEngine;

public class BreathingGreen : MonoBehaviour
{
    [Tooltip("Renderer whose material will be animated.")]
    [SerializeField] private Renderer targetRenderer;

    [Tooltip("Name of the material to animate (as shown in the renderer's materials list). Case-sensitive.")]
    [SerializeField] private string materialName = "";

    [Tooltip("Name of the color property on the material.")]
    [SerializeField] private string colorProperty = "_BaseColor";

    [Tooltip("Seconds to wait before the breathing effect starts.")]
    [SerializeField] private float delay = 0f;

    [Tooltip("How many full breaths (in + out) to complete before resetting.")]
    [SerializeField] private int breathCount = 3;

    [Tooltip("Duration of one full breath (in + out) in seconds.")]
    [SerializeField] private float breathDuration = 2f;

    [Tooltip("The green color at peak brightness.")]
    [SerializeField] private Color peakColor = new Color(0f, 1f, 0f, 1f);

    [Tooltip("The green color at minimum brightness.")]
    [SerializeField] private Color troughColor = new Color(0f, 0.15f, 0f, 1f);

    private Color     _originalColor;
    private Material  _targetMaterial;
    private Coroutine _coroutine;
    private int       _propertyId;

    private void Awake()
    {
        _propertyId = Shader.PropertyToID(colorProperty);

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();
    }

    /// <summary>
    /// Start the breathing effect. Waits for 'delay' seconds, then loops the
    /// breathing animation for 'duration' seconds and resets to the original color.
    /// Safe to call while already running — restarts it.
    /// </summary>
    public void Play()
    {
        if (targetRenderer == null) { Debug.LogWarning("[BreathingGreen] No renderer assigned."); return; }
        int idx = FindMaterialIndex();
        if (idx < 0) return;

        if (_coroutine != null)
            StopCoroutine(_coroutine);

        _targetMaterial = targetRenderer.materials[idx]; // cache instance — avoids new array copy every frame
        _originalColor  = _targetMaterial.GetColor(_propertyId);
        _coroutine = StartCoroutine(Run());
    }

    /// <summary>Stop immediately and reset to original color.</summary>
    public void Stop()
    {
        if (_coroutine != null)
        {
            StopCoroutine(_coroutine);
            _coroutine = null;
        }

        ResetColor();
    }

    private IEnumerator Run()
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float halfBreath = breathDuration * 0.5f;

        for (int i = 0; i < breathCount; i++)
        {
            // Fade in — trough → peak
            float elapsed = 0f;
            while (elapsed < halfBreath)
            {
                float t = elapsed / halfBreath;
                _targetMaterial.SetColor(_propertyId, Color.Lerp(troughColor, peakColor, t));
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Fade out — peak → trough
            elapsed = 0f;
            while (elapsed < halfBreath)
            {
                float t = elapsed / halfBreath;
                _targetMaterial.SetColor(_propertyId, Color.Lerp(peakColor, troughColor, t));
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        ResetColor();
        _coroutine = null;
    }

    private int FindMaterialIndex()
    {
        var mats = targetRenderer.materials;
        for (int i = 0; i < mats.Length; i++)
            if (mats[i].name.Replace(" (Instance)", "") == materialName)
                return i;
        Debug.LogWarning($"[BreathingGreen] Material '{materialName}' not found on '{targetRenderer.gameObject.name}'.");
        return -1;
    }

    private void ResetColor()
    {
        if (_targetMaterial != null)
            _targetMaterial.SetColor(_propertyId, _originalColor);
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(BreathingGreen))]
public class BreathingGreenEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        UnityEditor.EditorGUILayout.Space(8);
        var t = (BreathingGreen)target;
        if (GUILayout.Button("Play"))  t.Play();
        if (GUILayout.Button("Stop"))  t.Stop();
    }
}
#endif