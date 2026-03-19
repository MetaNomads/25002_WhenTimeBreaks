using System.Collections.Generic;
using UnityEngine;

public class C2Spawner : MonoBehaviour
{
    [SerializeField] private GameObject source;

    [Tooltip("Where to spawn. Falls back to this transform if left empty.")]
    [SerializeField] private GameObject spawnPoint;

    [Tooltip("Name of the child GameObject to disable after spawning.")]
    [SerializeField] private string childToDisableName;

    private readonly List<GameObject> _instances = new();

    // ── Spawn position / rotation — uses spawnPoint if assigned ──────────────────

    private Vector3    SpawnPosition => spawnPoint != null ? spawnPoint.transform.position : transform.position;
    private Quaternion SpawnRotation => spawnPoint != null ? spawnPoint.transform.rotation : transform.rotation;

    // ── Public API ────────────────────────────────────────────────────────────────

    public void Spawn()
    {
        if (source == null) return;

        var instance = Instantiate(source, SpawnPosition, SpawnRotation);
        instance.SetActive(true);

        if (!string.IsNullOrEmpty(childToDisableName))
        {
            var child = instance.transform.Find(childToDisableName);
            if (child != null)
                child.gameObject.SetActive(false);
            else
                Debug.LogWarning($"[C2Spawner] Child '{childToDisableName}' not found on spawned instance.", instance);
        }

        _instances.Add(instance);
    }

    public void FallAll()
    {
        foreach (var instance in _instances)
        {
            if (instance == null) continue;
            var rb  = instance.GetComponent<Rigidbody>();
            var col = instance.GetComponent<Collider>();
            if (rb  != null) rb.isKinematic = false;
            if (col != null) col.isTrigger  = false;
        }
    }

    public void DestroyAll()
    {
        foreach (var instance in _instances)
            if (instance != null) Destroy(instance);
        _instances.Clear();
    }
}