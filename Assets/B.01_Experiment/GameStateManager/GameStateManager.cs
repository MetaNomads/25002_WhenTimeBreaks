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

    //Keeps track of the current session
    public enum SessionType
    {
        TUTORIAL,
        SESSION_A,
        SESSION_B,
        SESSION_C
    }
    [SerializeField]
    private SessionType currentSessionType = SessionType.TUTORIAL;

    //Keeps track of the current trial
    private int trialNumber = 0;

    public enum TrialType
    {
        BEFORE_EXPERIMENT,
        NORMAL,
        ANOMOLY_1_OPTION_1,
        ANOMOLY_1_OPTION_2,
        AMOMOLY_1_OPTION_3,
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
    }


    [System.Serializable]
    public struct SequenceData
    {
        public List<TrialData> trialData;
    }

    //Struct which holds Session Sequences
    [System.Serializable]
    public struct SessionData
    {
        public SessionType sessionType;
        public List<SequenceData> sequences;
    }

    //Holds all the Sessions' Datas
    [SerializeField]
    private List<SessionData> SessionSequences = new List<SessionData>();

    //Event which signals anytime a new trial begins
    public event GameState GameStateTrigger;
    public delegate void GameState(SessionType sessionType, int trialNumber, TrialData trialData);

    //Event which signals every time a trial state is triggers (ex. picking up the cup, cup placed at target, etc.)
    public event TrialStateUpdate TrialStatesTrigger;
    public delegate void TrialStateUpdate(SessionType sessionType, int trialNumber, TrialStates trialState);


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
        currentSessionType = sessionType;
        currentTrialList = getRandomSequenceFromSession(currentSessionType);
    }

    private List<TrialData> getRandomSequenceFromSession(SessionType sessionType)
    {
        switch (sessionType) {
            case SessionType.TUTORIAL:
                return SessionSequences[0].sequences[UnityEngine.Random.Range(0, SessionSequences[0].sequences.Count)].trialData;
            case SessionType.SESSION_A:
                return SessionSequences[1].sequences[UnityEngine.Random.Range(0, SessionSequences[1].sequences.Count)].trialData;
            case SessionType.SESSION_B:
                return SessionSequences[2].sequences[UnityEngine.Random.Range(0, SessionSequences[2].sequences.Count)].trialData;
            case SessionType.SESSION_C:
                return SessionSequences[3].sequences[UnityEngine.Random.Range(0, SessionSequences[3].sequences.Count)].trialData;

        }

        Debug.LogError("Session Type is not registered in getRandomSequenceFromSession()");
        return null;

    }

    public void ProgressTrial()
    {
        if (experimentInProgress == false)
        {
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
        trialNumber += 1;

        Debug.Log("GSM: Trial " + trialNumber + " Is Starting.");

        //Update start time
        TrialData trialData = currentTrialList[trialNumber];
        trialData.trialStartTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        currentTrialList[trialNumber] = trialData;

        //Send event signal
        GameStateTrigger.Invoke(currentSessionType, trialNumber, currentTrialList[trialNumber]);

        spawnMechanism.SpawnCup();
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
}
