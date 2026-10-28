using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class Tuomari : MonoBehaviour
{
    [Header("Winner Text")]
    public TMP_Text resultText;
    public GameObject victoryPanel;

    [Header("Lap Times")]
    public int kierrostenMaara = 3;
    public TMP_Text kierros1Text;
    public TMP_Text kierros2Text;
    public TMP_Text kierros3Text;

    [Header("Final Result")]
    public TMP_Text lapsText;
    public TMP_Text racetimeText;
    public TMP_Text parasIkinAikaText;

    [Header("Victory Camera")]
    public Transform victoryCamera;
    public float victoryCameraHeight = 15f;
    public float victoryCameraTransitionDuration = 2.2f;
    public float victoryCameraArcHeight = 2f;
    public float victoryCameraFollowSpeed = 7f;

    [Header("Return To Main Menu")]
    public float backToMenuDelay = 10f;
    [Tooltip("Scene to load after the countdown.")]
    public string mainMenuSceneName = "StartScreen";

    private bool winnerDeclared = false;
    private bool raceTimerStarted = false;
    private float raceStartTime;
    private float previousLapTime;
    private const string BestTimeKey = "Ajopeli_BestTime";
    private Coroutine victoryCameraRoutine;
    private LookBackCamera lookBackCameraController;

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

            if (racetimeText != null)
                racetimeText.text = $"Race time: {totalTimeString}";

            if (victoryPanel != null)
                victoryPanel.SetActive(true);

            UpdateBestTime(totalTime);

            if (GameManager.Instance != null)
                GameManager.Instance.Phase = RacePhase.Finished;

            Debug.Log($"WINNER: {winnerName}, Race time: {totalTimeString}");

            if (id.kind == CarKind.Player)
                ShowVictoryCamera(id.transform);

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

    private void ShowVictoryCamera(Transform playerTransform)
    {
        Transform aiTransform = FindAiCarTransform(playerTransform);
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

        if (victoryCameraRoutine != null)
            StopCoroutine(victoryCameraRoutine);

        lookBackCameraController = null;
        LookBackCamera[] cameraControllers = Object.FindObjectsByType<LookBackCamera>(FindObjectsSortMode.None);
        foreach (LookBackCamera controller in cameraControllers)
        {
            if (controller != null && controller.carCamera == cam)
            {
                lookBackCameraController = controller;
                controller.enabled = false;
            }
        }

        cam.SetParent(null, true);
        victoryCameraRoutine = StartCoroutine(AnimateVictoryCamera(cam, aiTransform));
    }

    private IEnumerator AnimateVictoryCamera(Transform cam, Transform aiTransform)
    {
        Vector3 startPosition = cam.position;
        Quaternion startRotation = cam.rotation;
        float duration = Mathf.Max(0.1f, victoryCameraTransitionDuration);
        float elapsed = 0f;

        while (elapsed < duration && cam != null && aiTransform != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            Vector3 targetPosition = aiTransform.position + Vector3.up * victoryCameraHeight;
            Quaternion targetRotation = GetTopDownRotation(aiTransform);
            Vector3 position = Vector3.Lerp(startPosition, targetPosition, eased);
            position += Vector3.up * (Mathf.Sin(t * Mathf.PI) * victoryCameraArcHeight);
            cam.SetPositionAndRotation(position, Quaternion.Slerp(startRotation, targetRotation, eased));
            yield return null;
        }

        if (cam == null || aiTransform == null)
        {
            victoryCameraRoutine = null;
            yield break;
        }

        cam.SetPositionAndRotation(
            aiTransform.position + Vector3.up * victoryCameraHeight,
            GetTopDownRotation(aiTransform));

        while (cam != null && aiTransform != null)
        {
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, victoryCameraFollowSpeed) * Time.unscaledDeltaTime);
            Vector3 targetPosition = aiTransform.position + Vector3.up * victoryCameraHeight;
            Quaternion targetRotation = GetTopDownRotation(aiTransform);
            cam.position = Vector3.Lerp(cam.position, targetPosition, blend);
            cam.rotation = Quaternion.Slerp(cam.rotation, targetRotation, blend);
            yield return null;
        }

        victoryCameraRoutine = null;
    }

    private Quaternion GetTopDownRotation(Transform aiTransform)
    {
        Vector3 screenUp = Vector3.ProjectOnPlane(aiTransform.forward, Vector3.up);
        if (screenUp.sqrMagnitude < 0.0001f)
            screenUp = Vector3.forward;

        return Quaternion.LookRotation(Vector3.down, screenUp.normalized);
    }

    private Transform FindAiCarTransform(Transform referenceTransform)
    {
        CarIdentify[] cars = Object.FindObjectsByType<CarIdentify>(FindObjectsSortMode.None);
        Transform closestAi = null;
        float closestDistance = float.MaxValue;

        foreach (CarIdentify car in cars)
        {
            if (car == null || car.kind != CarKind.AI || !car.gameObject.activeInHierarchy)
                continue;

            float distance = referenceTransform != null
                ? (car.transform.position - referenceTransform.position).sqrMagnitude
                : 0f;

            if (closestAi == null || distance < closestDistance)
            {
                closestAi = car.transform;
                closestDistance = distance;
            }
        }

        return closestAi;
    }

    private Transform FindPlayerCameraTransform()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Camera[] cameras = player.GetComponentsInChildren<Camera>(true);
            foreach (Camera cam in cameras)
            {
                if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
                    return cam.transform;
            }

            if (cameras.Length > 0 && cameras[0] != null)
                return cameras[0].transform;
        }

        return null;
    }
}
