using UnityEngine;
using MetaFrame.State;

public class ObjectReference : MonoBehaviour
{
    [SerializeField] private GameObject reference;

    [SerializeField] private StateDefinition targetState;

    public GameObject Get() => reference;

    public void RequestStateTransition()
    {
        if (reference == null) { Debug.LogError("[ObjectReference] No reference assigned."); return; }

        var gsm = reference.GetComponent<GameStateManager>();
        if (gsm == null) { Debug.LogError($"[ObjectReference] No GameStateManager found on '{reference.name}'."); return; }
        if (targetState == null) { Debug.LogError("[ObjectReference] No target state assigned."); return; }

        gsm.RequestTransition(targetState);
    }
}