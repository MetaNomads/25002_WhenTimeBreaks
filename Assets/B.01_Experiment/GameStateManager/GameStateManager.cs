using MetaFrame.Data;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static GameStateManager;
using System;
using static MetaFrame.Data.SurveyDataRecorder;


public class GameStateManager : MonoBehaviour
{

    public static GameStateManager instance;


    [Header("Reference Scripts")]
    [SerializeField]
    private SurveyDataRecorder surveyDataRecorder;
    [SerializeField]
    private SpawnMechanism spawnMechanism;



    //Keeps track of the current trial
    private int trialNumber = 0;

    public enum TrialType
    {
        BEFORE_EXPERIMENT,
        NORMAL,
        ANOMOLY_1_OPTION_1,
        ANOMOLY_1_OPTION_2,
        AMOMOLY_1_OPTION_3,
        ANOMOLY_2_OPTION_1,
        ANOMOLY_2_OPTION_2,
        ANOMOLY_2_OPTION_3,

        //Add rest

        CANCEL //Unsure if we need this one but just to be safe I left it as an option
    }

    //Different states that occur during each trial
    public enum TrialStates
    {
        AT_SOURCE,
        IN_HAND,
        AT_TARGET
    }

    //Struct which holds the trial data

    [System.Serializable]
    public struct TrialData
    {
        [Tooltip("Set the state for each trial to the anomoly you want to occur during this trial.")]
        public TrialType state;
        [System.NonSerialized]
        public string trialStartTime;
        [System.NonSerialized]
        public string trialEndTime;
        [System.NonSerialized]
        public string objectAtSource;
        [System.NonSerialized]
        public string objectInHand;
        [System.NonSerialized]
        public string objectAtTarget;
    }


    [System.Serializable]
    public struct SequenceData
    {
        public List<TrialData> trialData;
    }

    //Keeps track of the session types
    public enum SessionType
    {
        TUTORIAL,
        SESSION_A,
        SESSION_B,
        SESSION_C,
    }


    //Struct which holds Session Sequences
    [System.Serializable]
    public struct SessionData
    {
        public SessionType sessionType;
        public List<SequenceData> sequences;
        [System.NonSerialized]
        public int currentSequence;
    }

    //Holds all the Sessions' Datas
    [SerializeField]
    private List<SessionData> SessionSequences = new List<SessionData>();

    //Holds the info and data for the current session
    [SerializeField]
    private SessionData? currentSessionData = null;

    //Event which signals anytime a new trial begins
    public event GameState GameStateTrigger;
    public delegate void GameState(SessionData? sessionType, int trialNumber, TrialData trialData);

    //Event which signals every time a trial state is triggers (ex. picking up the cup, cup placed at target, etc.)
    public event TrialStateUpdate TrialStatesTrigger;
    public delegate void TrialStateUpdate(SessionData? sessionType, int trialNumber, TrialStates trialState);


    //Keeps track of if an experiment is currently in progress
    private bool experimentInProgress = false;

    //Current trial list sequence being run
    private List<TrialData> currentTrialList;

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogWarning("A second GameStateManager was detected and deleted!");
            Destroy(gameObject);
        }
        instance = this;
    }

    public void StartSessionTutorial()
    {
        UpdateSessionType(SessionType.TUTORIAL);
    }

    public void StartSessionA()
    {
        UpdateSessionType(SessionType.SESSION_A);
    }

    public void StartSessionB()
    {
        UpdateSessionType(SessionType.SESSION_B);
    }

    public void StartSessionC()
    {
        UpdateSessionType(SessionType.SESSION_C);
    }

    public void UpdateSessionType(SessionType sessionType)
    {
        currentSessionData = MatchSessionTypeToData(sessionType);
        currentTrialList = getRandomSequenceFromSession(sessionType);
    }

    public SessionData? MatchSessionTypeToData(SessionType sessionType)
    {
        switch (sessionType)
        {
            case SessionType.TUTORIAL:
                return SessionSequences[0];
            case SessionType.SESSION_A:
                return SessionSequences[1];
            case SessionType.SESSION_B:
                return SessionSequences[2];
            case SessionType.SESSION_C:
                return SessionSequences[3];
            default:
                Debug.LogError("Session type not found!");
                return null;
        }
    }

    private List<TrialData> getRandomSequenceFromSession(SessionType sessionType)
    {

        //Note: MatchSessionTypeToData() was made after and could be used to clean up this function

        int selectedSequence = -100;
        SessionData tempSessionData;

        switch (sessionType) {
            case SessionType.TUTORIAL:
                selectedSequence = UnityEngine.Random.Range(0, SessionSequences[0].sequences.Count);
                tempSessionData = SessionSequences[0];
                tempSessionData.currentSequence = selectedSequence;
                SessionSequences[0] = tempSessionData;
                return SessionSequences[0].sequences[selectedSequence].trialData;
            case SessionType.SESSION_A:
                selectedSequence = UnityEngine.Random.Range(0, SessionSequences[1].sequences.Count);
                tempSessionData = SessionSequences[1];
                tempSessionData.currentSequence = selectedSequence;
                SessionSequences[1] = tempSessionData;
                return SessionSequences[1].sequences[selectedSequence].trialData;
            case SessionType.SESSION_B:
                selectedSequence = UnityEngine.Random.Range(0, SessionSequences[2].sequences.Count);
                tempSessionData = SessionSequences[2];
                tempSessionData.currentSequence = selectedSequence;
                SessionSequences[2] = tempSessionData;
                return SessionSequences[2].sequences[selectedSequence].trialData;
            case SessionType.SESSION_C:
                selectedSequence = UnityEngine.Random.Range(0, SessionSequences[3].sequences.Count);
                tempSessionData = SessionSequences[3];
                tempSessionData.currentSequence = selectedSequence;
                SessionSequences[3] = tempSessionData;
                return SessionSequences[3].sequences[selectedSequence].trialData;

        }


        Debug.LogError("Session Type is not registered in getRandomSequenceFromSession()");
        return null;

    }

    public void ProgressTrial()
    {
        if (experimentInProgress == false)
        {

            if (currentSessionData == null)
            {
                Debug.LogError("Session type not set. Will not begin experiment");
                return;
            }

            BeginNextTrial();
            experimentInProgress = true;
            Debug.Log("GSM: Experiment started");
        }
        else
        {
            UpdateDataThenBeginNextTrial(surveyDataRecorder.stateD);
        }
    }

    public void BeginNextTrial()
    {
        if (trialNumber >= currentTrialList.Count)
        {
            Debug.LogWarning("No more trials available!");
            return;
        }


        //Update the current state
        if (checkIfTrialCanContinue())
        {
            trialNumber += 1;
        }
        else
        {
            return;
        }

        Debug.Log("GSM: Trial " + trialNumber + " Is Starting.");

        //Update start time
        TrialData trialData = currentTrialList[trialNumber];
        trialData.trialStartTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        currentTrialList[trialNumber] = trialData;

        //Send event signal
        GameStateTrigger.Invoke(currentSessionData, trialNumber, currentTrialList[trialNumber]);

        spawnMechanism.SpawnCup();
    }

    private bool checkIfTrialCanContinue()
    {
        if ((trialNumber + 1) < currentTrialList.Count)
        {
            return true;
        }
        ConcludeSession();
        return false;
    }

    private void ConcludeSession()
    {
        Debug.Log("GSM: Session concluded");
        experimentInProgress = false;
        currentSessionData = null;
        currentTrialList = null;
        //Do other things here to tell the participant their session has concluded.
    }



    public void UpdateDataThenBeginNextTrial(StateData stateData)
    {
        //Update trial end time
        TrialData trialData = currentTrialList[trialNumber];
        trialData.trialEndTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        currentTrialList[trialNumber] = trialData;

        //Add the trial start and end time to the surveyData
        StateData stateDataUpdated = stateData;
        //stateDataUpdated.triallNo = trialNumber.ToString();
        //stateDataUpdated.triall_S = trialData.trialStartTime;
        //stateDataUpdated.triall_E = trialData.trialEndTime;
        //stateDataUpdated.placed = spawnMechanism.CupPlacementCompletion();
        //surveyDataRecorder.StoreToggleValues();
        //Debug.Log("GSM Survey Data: " + surveyDataRecorder.stateD.triallNo + " " + surveyDataRecorder.surveyD.detection + " " + surveyDataRecorder.surveyD.confidence + " " + surveyDataRecorder.surveyD.explanation + " " + surveyDataRecorder.stateD.triall_S + " " + surveyDataRecorder.surveyD.report_S + " " + surveyDataRecorder.stateD.triall_E + " " + surveyDataRecorder.stateD.placed);
        EndCurrentTrial();
        BeginNextTrial();
    }

    public void EndCurrentTrial()
    {
        spawnMechanism.DestroyCup();
        Debug.Log("GSM: Ending trial " + trialNumber);
    }


    /* Signals for the trial! Make sure to call these to the objects using AnomolyReciever know when these events occur! */
    public void HandGrabbedObjectSignal()
    {
        TrialData trialData = currentTrialList[trialNumber];
        trialData.objectInHand = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        currentTrialList[trialNumber] = trialData;

        TrialStatesTrigger.Invoke(currentSessionData, trialNumber, TrialStates.IN_HAND);
    }

    public void ObjectReachedSourceSignal()
    {
        TrialData trialData = currentTrialList[trialNumber];
        trialData.objectAtSource = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        currentTrialList[trialNumber] = trialData;

        TrialStatesTrigger.Invoke(currentSessionData, trialNumber, TrialStates.AT_SOURCE);
    }

    public void ObjectReachedTargetSignal()
    {
        TrialData trialData = currentTrialList[trialNumber];
        trialData.objectAtTarget = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        currentTrialList[trialNumber] = trialData;

        TrialStatesTrigger.Invoke(currentSessionData, trialNumber, TrialStates.AT_TARGET);
    }

    /* Get functions for the GameStateManager for data collection */
    public SessionData? GetCurrentSessionData()
    {
        GameStateManager.instance.GetCurrentSessionData();
        return currentSessionData;
    }

    public TrialData? GetCurrentTrialData()
    {
        return currentTrialList[trialNumber];
    }

    public int GetTrialNumber()
    {
        return trialNumber;
    }
}
