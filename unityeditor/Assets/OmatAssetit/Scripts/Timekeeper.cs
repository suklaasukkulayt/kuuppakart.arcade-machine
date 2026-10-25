using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class Tuomari : MonoBehaviour
{
    [Header("Winner Text")]
    public TMP_Text resultText;

    [Header("Lap Times")]
    public int kierrostenMaara = 3;
    public TMP_Text kierros1Text;
    public TMP_Text kierros2Text;
    public TMP_Text kierros3Text;

    [Header("Final Result")]
    public TMP_Text lapsText;
    public TMP_Text parasIkinAikaText;

    [Header("Victory Camera (when PLAYER wins)")]
    public Transform victoryCamera;

    public float victoryCameraHeight = 15f;

    [Header("Return To Main Menu")]
    public float backToMenuDelay = 10f;
    [Tooltip("Scene to load after the countdown.")]
    public string mainMenuSceneName = "StartScreen";

    private bool winnerDeclared = false;
    private bool raceTimerStarted = false;
    private float raceStartTime;
    private float previousLapTime;
    private const string BestTimeKey = "Ajopeli_BestTime";

    private void Start()
    {
        winnerDeclared = false;
        raceTimerStarted = false;
        previousLapTime = 0f;

        if (resultText != null)
            resultText.text = "";

        SetLapTextVisible(kierros1Text, true);
        SetLapTextVisible(kierros2Text, true);
        SetLapTextVisible(kierros3Text, true);

        SetBestTimeVisible(false);

        if (lapsText == null)
            lapsText = FindLapsText();
    }

    private void Update()
    {
        if (!raceTimerStarted && GameManager.Instance != null && GameManager.Instance.Phase == RacePhase.Racing)
        {
            raceTimerStarted = true;
            raceStartTime = Time.time;
            previousLapTime = 0f;
        }
    }

    private void OnTriggerEnter(Collider car)
    {
        CarIdentify id = car.GetComponent<CarIdentify>();
        if (id == null)
            return;

        LapCounter lap = car.GetComponent<LapCounter>();
        if (lap == null)
        {
            Debug.LogWarning("[Tuomari] Car is missing LapCounter.");
            return;
        }

        if (id.kind == CarKind.AI)
        {
            AICar ai = car.GetComponent<AICar>();
            if (ai != null)
            {
                if (!ai.LapArmed)
                    return;

                PelaajanKierrosTarkastus aiValidator = car.GetComponent<PelaajanKierrosTarkastus>();
                if (aiValidator != null)
                {
                    if (!aiValidator.AllVisitedThisLap)
                    {
                        Debug.Log("AI crossed the finish line, but hasn't hit all the checkpoints!");
                        return;
                    }

                    aiValidator.ResetLap();
                }

                ai.ConsumeLap();
            }
        }

        if (id.kind == CarKind.Player)
        {
            var validator = car.GetComponent<PelaajanKierrosTarkastus>();
            if (validator == null)
            {
                Debug.LogError("PelaajanKierrosTarkastus component is missing.");
                return;
            }

            if (!validator.AllVisitedThisLap)
            {
                Debug.Log("Player crossed the finish line, but hasn't hit all the checkpoints!");
                validator.ShowMissedCheckpointsMessage();
                return;
            }

            int completedLapNumber = lap.lapsCompleted + 1;
            RecordPlayerLapTime(completedLapNumber);

            validator.UpdateLapsText(completedLapNumber, kierrostenMaara);
            validator.ResetLap();
        }

        lap.lapsCompleted++;

        if (!winnerDeclared && lap.lapsCompleted >= kierrostenMaara)
        {
            string winnerName = id.displayName;
            winnerDeclared = true;

            float totalTime = raceTimerStarted ? Time.time - raceStartTime : 0f;
            string totalTimeString = FormatTime(totalTime);

            if (resultText != null)
                resultText.text = $"<mark>WINNER: {winnerName}</mark>";

            if (lapsText == null)
                lapsText = FindLapsText();

            if (lapsText != null)
                lapsText.text = $"Race time: {totalTimeString}";

            UpdateBestTime(totalTime);

            if (GameManager.Instance != null)
                GameManager.Instance.Phase = RacePhase.Finished;

            Debug.Log($"WINNER: {winnerName}, Race time: {totalTimeString}");

            if (id.kind == CarKind.Player)
                ShowAiGapCamera();

            StartCoroutine(BackToMainMenuCountdown());
        }
    }

    private void RecordPlayerLapTime(int lapNumber)
    {
        if (!raceTimerStarted)
        {
            raceTimerStarted = true;
            raceStartTime = Time.time;
            previousLapTime = 0f;
        }

        float totalElapsed = Time.time - raceStartTime;
        float lapTime = totalElapsed - previousLapTime;
        previousLapTime = totalElapsed;

        SetLapTextForNumber(lapNumber, lapTime);
    }

    private void SetLapTextForNumber(int lapNumber, float lapTime)
    {
        TMP_Text target = null;

        switch (lapNumber)
        {
            case 1: target = kierros1Text; break;
            case 2: target = kierros2Text; break;
            case 3: target = kierros3Text; break;
        }

        if (target != null)
        {
            target.text = $"Lap {lapNumber}: {FormatTime(lapTime)}";
            SetLapTextVisible(target, true);
        }
    }

    private void SetLapTextVisible(TMP_Text target, bool visible)
    {
        if (target != null)
            target.enabled = visible;
    }

    private string FormatTime(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;

        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainingSeconds = seconds - minutes * 60f;
        return $"{minutes:00}:{remainingSeconds:00.000}";
    }

    private void UpdateBestTime(float totalTime)
    {
        if (totalTime <= 0f)
            return;

        float currentBest = PlayerPrefs.GetFloat(BestTimeKey, -1f);

        if (currentBest < 0f || totalTime < currentBest)
        {
            PlayerPrefs.SetFloat(BestTimeKey, totalTime);
            PlayerPrefs.Save();
            currentBest = totalTime;
        }

        if (parasIkinAikaText != null)
        {
            parasIkinAikaText.text = $"<mark>Best time: {FormatTime(currentBest)}</mark>";
            SetBestTimeVisible(true);
        }
    }

    private void SetBestTimeVisible(bool visible)
    {
        if (parasIkinAikaText != null && parasIkinAikaText.gameObject != null)
            parasIkinAikaText.gameObject.SetActive(visible);
    }

    private IEnumerator BackToMainMenuCountdown()
    {
        TMP_Text boostText = FindBoostText();
        int secondsLeft = Mathf.CeilToInt(backToMenuDelay);

        while (secondsLeft > 0)
        {
            if (boostText != null)
                boostText.text = $"Returning to main menu in {secondsLeft}s";

            yield return new WaitForSeconds(1f);
            secondsLeft--;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private TMP_Text FindBoostText()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Player playerScript = player.GetComponent<Player>();
            if (playerScript != null)
                return playerScript.uiText;
        }

        return null;
    }

    private TMP_Text FindLapsText()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            PelaajanKierrosTarkastus validator = player.GetComponent<PelaajanKierrosTarkastus>();
            if (validator != null)
                return validator.lapsText;
        }

        return null;
    }

    private void ShowAiGapCamera()
    {
        Transform aiTransform = FindAiCarTransform();
        if (aiTransform == null)
        {
            Debug.LogWarning("[Tuomari] AI car not found for victory camera.");
            return;
        }

        Transform cam = victoryCamera != null ? victoryCamera : FindPlayerCameraTransform();
        if (cam == null)
        {
            Debug.LogWarning("[Tuomari] Camera not found for victory camera.");
            return;
        }

        cam.SetParent(null, true);
        cam.position = aiTransform.position + Vector3.up * victoryCameraHeight;
        cam.rotation = Quaternion.LookRotation(Vector3.down, aiTransform.forward);
    }

    private Transform FindAiCarTransform()
    {
        CarIdentify[] cars = Object.FindObjectsByType<CarIdentify>(FindObjectsSortMode.None);
        foreach (var c in cars)
        {
            if (c.kind == CarKind.AI)
                return c.transform;
        }

        return null;
    }

    private Transform FindPlayerCameraTransform()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Camera cam = player.GetComponentInChildren<Camera>();
            if (cam != null)
                return cam.transform;
        }

        return null;
    }
}