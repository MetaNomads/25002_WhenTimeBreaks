using System.Collections.Generic;
using UnityEngine;

public class FloatingSpawner : MonoBehaviour
{
    [SerializeField] private GameObject source;

    private readonly List<GameObject> _instances = new();

    public void Spawn()
    {
        if (source == null) return;
        var instance = Instantiate(source, transform.position, transform.rotation);
        var rb       = instance.GetComponent<Rigidbody>();
        var col      = instance.GetComponent<Collider>();
        if (rb  != null) rb.isKinematic = true;
        if (col != null) col.isTrigger  = true;
        instance.SetActive(true);
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