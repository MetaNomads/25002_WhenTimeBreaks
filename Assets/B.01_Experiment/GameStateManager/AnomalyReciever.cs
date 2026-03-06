using UnityEngine;
using static GameStateManager;

public class AnomalyReciever : MonoBehaviour
{

    [SerializeField]
    protected TrialType AnomalyToRecieve = TrialType.NORMAL;

    private bool AnomalyCurrentlyEnabled = false;

    private void Start()
    {
        //Subscribe to the GameStateManager
        GameStateManager.instance.GameStateTrigger += GameStateTrigger;
        GameStateManager.instance.TrialStatesTrigger += TrialStateUpdated;

        SetupObjectAtStart();
    }

    protected virtual void SetupObjectAtStart()
    {
        //Manage this from inheriting script
    }

    private void GameStateTrigger(SessionType sessionType, int trialNumber, TrialData trialData)
    {


        if (trialData.state == AnomalyToRecieve)
        {
            AnomalyCurrentlyEnabled = true;
            AnomalyEnabled();



        }
        else
        {
            if (AnomalyCurrentlyEnabled == true)
            {
                AnomalyDisabled();
                AnomalyCurrentlyEnabled = false;
            }
        }



    }

    private void TrialStateUpdated(SessionType sessionType, int trialNumber, TrialStates trialState)
    {
        if (AnomalyCurrentlyEnabled)
        {

            switch (trialState)
            {

                case TrialStates.AT_SOURCE:
                    ObjectAtSource();
                    break;
                case TrialStates.AT_TARGET:
                    ObjectAtTarget();
                    break;
                case TrialStates.IN_HAND:
                    ObjectInHand();
                    break;
                default:
                    Debug.LogError("Unknown state passed to Anomaly reciever!");
                    break;

            }

        }


    }

    virtual protected void AnomalyEnabled()
    {
        //Manage this in the inheriting scripts
    }

    protected virtual void AnomalyDisabled()
    {

        //Manage this in the inheriting scripts


    }

    virtual protected void CancelAnomaly()
    {
        //Manage this in the inheriting scripts
    }


    virtual protected void ObjectAtSource()
    {
        //Manage this in inheriting scripts
    }

    virtual protected void ObjectInHand()
    {
        //Manage this in inheriting scripts
    }

    virtual protected void ObjectAtTarget()
    {
        //Manage this in inheriting scripts
    }


    private void OnDestroy()
    {
        //Unsubscribe from GameStateManager
        GameStateManager.instance.GameStateTrigger -= GameStateTrigger;
        GameStateManager.instance.TrialStatesTrigger -= TrialStateUpdated;

    }
}
