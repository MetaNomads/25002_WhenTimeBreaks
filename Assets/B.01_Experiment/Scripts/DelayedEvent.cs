using UnityEngine;
using UnityEngine.Events;

public class DelayedEvent : MonoBehaviour
{
    [SerializeField] private float       delay = 1f;

    [Space]
    public UnityEvent onStart;
    public UnityEvent onComplete;

    public void Play()
    {
        onStart?.Invoke();
        StartCoroutine(WaitThenComplete());
    }

    private System.Collections.IEnumerator WaitThenComplete()
    {
        yield return new WaitForSeconds(delay);
        onComplete?.Invoke();
    }
}