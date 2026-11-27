using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Text;

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
    public TMP_Text leaderboardText;

    [Header("Race HUD")]
    public TMP_Text raceTimeHudText;
    public TMP_Text playerPositionText;
    [Min(0.05f)] public float raceHudRefreshInterval = 0.2f;

    [Header("Victory Camera")]
    public Transform victoryCamera;
    public float victoryCameraHeight = 15f;
    public float victoryCameraTransitionDuration = 2.2f;
    public float victoryCameraArcHeight = 2f;
    public float victoryCameraFollowSpeed = 7f;

    [Header("Return To Main Menu")]
    public float backToMenuDelay = 10f;
    public string mainMenuSceneName = "StartScreen";

    private bool winnerDeclared;
    private bool raceTimerStarted;
    private float raceStartTime;
    private float previousLapTime;
    private float finalRaceTime;
    private float hudRefreshTimer;
    private const string BestTimeKey = "Ajopeli_BestTime";
    private Coroutine victoryCameraRoutine;
    private LookBackCamera lookBackCameraController;
    private CheckpointTarkistus[] orderedCheckpoints = new CheckpointTarkistus[0];
    private readonly Dictionary<CarIdentify, float> finishTimes = new Dictionary<CarIdentify, float>();
    private readonly Dictionary<CarIdentify, string> carDisplayNames = new Dictionary<CarIdentify, string>();
    private readonly Dictionary<string, int> aiModelNameCounts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

    private class RaceStanding
    {
        public CarIdentify car;
        public float progress;
        public float finishTime;
        public bool hasFinished;
    }

    private void Start()
    {
        winnerDeclared = false;
        raceTimerStarted = false;
        previousLapTime = 0f;
        finalRaceTime = 0f;
        hudRefreshTimer = 0f;
        finishTimes.Clear();
        CacheCheckpoints();
        CacheCarDisplayNames();

        if (resultText != null)
            resultText.text = "";

        if (leaderboardText != null)
            leaderboardText.text = "";

        SetLapTextVisible(kierros1Text, true);
        SetLapTextVisible(kierros2Text, true);
        SetLapTextVisible(kierros3Text, true);
        SetBestTimeVisible(false);
        SetRaceHudVisible(false);

        if (lapsText == null)
            lapsText = FindLapsText();
    }

    private void Update()
    {
        if (!raceTimerStarted && GameManager.Instance != null && GameManager.Instance.Phase == RacePhase.Racing)
            StartRaceTimer();

        if (!raceTimerStarted)
            return;

        float elapsed = winnerDeclared ? finalRaceTime : Time.time - raceStartTime;
        if (raceTimeHudText != null)
            raceTimeHudText.text = $"{FormatTime(elapsed)}";

        if (winnerDeclared || GameManager.Instance == null || GameManager.Instance.Phase != RacePhase.Racing)
            return;

        hudRefreshTimer -= Time.deltaTime;
        if (hudRefreshTimer <= 0f)
        {
            RefreshPlayerPosition();
            hudRefreshTimer = Mathf.Max(0.05f, raceHudRefreshInterval);
        }
    }

    private void OnTriggerEnter(Collider car)
    {
        if (winnerDeclared || GameManager.Instance == null || GameManager.Instance.Phase != RacePhase.Racing)
            return;

        CarIdentify id = car.GetComponent<CarIdentify>();
        if (id == null)
            return;

        LapCounter lap = car.GetComponent<LapCounter>();
        if (lap == null)
        {
            Debug.LogWarning("[Tuomari] Car is missing LapCounter.");
            return;
        }

        EnsureRaceTimerStarted();

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
            PelaajanKierrosTarkastus validator = car.GetComponent<PelaajanKierrosTarkastus>();
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

        if (lap.lapsCompleted >= kierrostenMaara)
        {
            RegisterFinish(id);

            if (id.kind == CarKind.AI)
            {
                FreezeFinishedAI(car);
                RefreshPlayerPosition();
                return;
            }

            if (id.kind == CarKind.Player)
                FinishRace(id);
        }
        else if (id.kind == CarKind.Player)
        {
            RefreshPlayerPosition();
        }
    }

    private void StartRaceTimer()
    {
        raceTimerStarted = true;
        raceStartTime = Time.time;
        previousLapTime = 0f;
        SetRaceHudVisible(true);
        RefreshPlayerPosition();
    }

    private void EnsureRaceTimerStarted()
    {
        if (!raceTimerStarted)
            StartRaceTimer();
    }

    private void RegisterFinish(CarIdentify car)
    {
        if (car == null || finishTimes.ContainsKey(car))
            return;

        finishTimes.Add(car, Time.time - raceStartTime);
    }

    private void FreezeFinishedAI(Collider car)
    {
        AICar ai = car.GetComponent<AICar>();
        if (ai != null)
            ai.enabled = false;

        Collider[] colliders = car.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
            collider.enabled = false;

        Rigidbody body = car.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }

    private void FinishRace(CarIdentify player)
    {
        winnerDeclared = true;
        finalRaceTime = finishTimes[player];

        List<RaceStanding> standings = GetStandings();
        if (standings.Count == 0)
            return;

        RaceStanding winner = standings[0];
        string winnerName = GetCarDisplayName(winner.car);
        string playerTimeString = FormatTime(finalRaceTime);

        if (resultText != null)
            resultText.text = $"<mark>WINNER: {winnerName}</mark>";

        if (lapsText == null)
            lapsText = FindLapsText();

        if (racetimeText != null)
            racetimeText.text = $"Race time: {playerTimeString}";

        if (leaderboardText != null)
            leaderboardText.text = BuildLeaderboardText(standings);

        if (victoryPanel != null)
            victoryPanel.SetActive(true);

        UpdateBestTime(finalRaceTime);
        RefreshPlayerPosition(standings);

        if (GameManager.Instance != null)
            GameManager.Instance.Phase = RacePhase.Finished;

        Debug.Log($"WINNER: {winnerName}, Player race time: {playerTimeString}");

        if (winner.car != null && winner.car.kind == CarKind.AI)
            ShowVictoryCamera(winner.car.transform);
        else if (player != null)
            ShowVictoryCamera(player.transform);

        StartCoroutine(BackToMainMenuCountdown());
    }

    private void CacheCheckpoints()
    {
        List<CheckpointTarkistus> checkpoints = new List<CheckpointTarkistus>(Object.FindObjectsByType<CheckpointTarkistus>(FindObjectsSortMode.None));
        checkpoints.RemoveAll(checkpoint => checkpoint == null);
        checkpoints.Sort((a, b) => a.orderIndex.CompareTo(b.orderIndex));
        orderedCheckpoints = checkpoints.ToArray();
    }

    private List<RaceStanding> GetStandings()
    {
        CarIdentify[] cars = Object.FindObjectsByType<CarIdentify>(FindObjectsSortMode.None);
        List<RaceStanding> standings = new List<RaceStanding>(cars.Length);

        foreach (CarIdentify car in cars)
        {
            if (car == null || !car.gameObject.activeInHierarchy)
                continue;

            bool hasFinished = finishTimes.TryGetValue(car, out float finishTime);
            RaceStanding standing = new RaceStanding
            {
                car = car,
                progress = GetRaceProgress(car),
                hasFinished = hasFinished,
                finishTime = hasFinished ? finishTime : 0f
            };

            standings.Add(standing);
        }

        standings.Sort(CompareStandings);
        return standings;
    }

    private int CompareStandings(RaceStanding a, RaceStanding b)
    {
        if (a.hasFinished != b.hasFinished)
            return a.hasFinished ? -1 : 1;

        if (a.hasFinished)
            return a.finishTime.CompareTo(b.finishTime);

        int progressComparison = b.progress.CompareTo(a.progress);
        if (progressComparison != 0)
            return progressComparison;

        return string.Compare(GetCarDisplayName(a.car), GetCarDisplayName(b.car), System.StringComparison.Ordinal);
    }

    private float GetRaceProgress(CarIdentify car)
    {
        LapCounter lap = car.GetComponent<LapCounter>();
        int completedLaps = lap != null ? Mathf.Max(0, lap.lapsCompleted) : 0;
        PelaajanKierrosTarkastus validator = car.GetComponent<PelaajanKierrosTarkastus>();

        if (validator == null || orderedCheckpoints.Length == 0)
            return completedLaps + 1f / (1f + Vector3.Distance(car.transform.position, transform.position));

        int checkpointCount = Mathf.Min(orderedCheckpoints.Length, validator.TotalCheckpointCount);
        if (checkpointCount <= 0)
            return completedLaps;

        int segmentIndex = Mathf.Clamp(validator.SequentialCheckpointCount, 0, checkpointCount);
        Vector3 segmentStart = segmentIndex == 0
            ? transform.position
            : orderedCheckpoints[segmentIndex - 1].transform.position;
        Vector3 segmentEnd = segmentIndex == checkpointCount
            ? transform.position
            : orderedCheckpoints[segmentIndex].transform.position;

        Vector3 segment = segmentEnd - segmentStart;
        float segmentLengthSquared = segment.sqrMagnitude;
        float segmentProgress = segmentLengthSquared > 0.001f
            ? Mathf.Clamp01(Vector3.Dot(car.transform.position - segmentStart, segment) / segmentLengthSquared)
            : 0f;

        return completedLaps + (segmentIndex + segmentProgress) / (checkpointCount + 1f);
    }

    private void RefreshPlayerPosition()
    {
        RefreshPlayerPosition(GetStandings());
    }

    private void RefreshPlayerPosition(List<RaceStanding> standings)
    {
        if (playerPositionText == null)
            return;

        RaceStanding playerStanding = null;
        int playerIndex = -1;

        for (int i = 0; i < standings.Count; i++)
        {
            if (standings[i].car != null && standings[i].car.kind == CarKind.Player)
            {
                playerStanding = standings[i];
                playerIndex = i;
                break;
            }
        }

        if (playerStanding == null)
            return;

        playerPositionText.text = $"POS: {playerIndex + 1}/{standings.Count}";
    }

    private string BuildLeaderboardText(List<RaceStanding> standings)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<size=120%><b>LEADERBOARD</b></size>");
        builder.AppendLine("<b>POS     CAR                         TIME</b>");

        for (int i = 0; i < standings.Count; i++)
        {
            RaceStanding standing = standings[i];
            string carName = GetCarDisplayName(standing.car);
            string time = standing.hasFinished
                ? FormatTime(standing.finishTime)
                : GetInProgressLabel(standing.car);

            builder.AppendLine($"{i + 1}.      {carName}     {time}");
        }

        return builder.ToString();
    }

    private string GetInProgressLabel(CarIdentify car)
    {
        LapCounter lap = car != null ? car.GetComponent<LapCounter>() : null;
        int currentLap = lap != null ? Mathf.Clamp(lap.lapsCompleted + 1, 1, kierrostenMaara) : 1;
        return $"Lap {currentLap}/{kierrostenMaara}";
    }

    private void CacheCarDisplayNames()
    {
        carDisplayNames.Clear();
        aiModelNameCounts.Clear();

        CarIdentify[] cars = Object.FindObjectsByType<CarIdentify>(FindObjectsSortMode.None);
        List<CarIdentify> activeCars = new List<CarIdentify>(cars.Length);

        foreach (CarIdentify car in cars)
        {
            if (car != null && car.gameObject.activeInHierarchy)
                activeCars.Add(car);
        }

        activeCars.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

        foreach (CarIdentify car in activeCars)
        {
            if (car.kind == CarKind.Player)
                carDisplayNames[car] = GetPlayerDisplayName(car);
        }

        foreach (CarIdentify car in activeCars)
        {
            if (car.kind == CarKind.AI)
                RegisterAIName(car);
        }
    }

    private void RegisterAIName(CarIdentify car)
    {
        if (car == null || carDisplayNames.ContainsKey(car))
            return;

        string modelName = GetAIModelName(car);
        aiModelNameCounts.TryGetValue(modelName, out int currentCount);
        int number = currentCount + 1;
        aiModelNameCounts[modelName] = number;
        carDisplayNames[car] = $"{modelName} {number}";
    }

    private string GetAIModelName(CarIdentify car)
    {
        string modelName = car.gameObject.name;
        int cloneSuffixIndex = modelName.IndexOf("(Clone)", System.StringComparison.Ordinal);

        if (cloneSuffixIndex >= 0)
            modelName = modelName.Substring(0, cloneSuffixIndex);

        modelName = modelName.Trim();
        return string.IsNullOrWhiteSpace(modelName) ? "AI Car" : modelName;
    }

    private string GetPlayerDisplayName(CarIdentify car)
    {
        return string.IsNullOrWhiteSpace(car.displayName) ? "Player" : car.displayName;
    }

    private string GetCarDisplayName(CarIdentify car)
    {
        if (car == null)
            return "Unknown";

        if (car.kind == CarKind.Player)
            return GetPlayerDisplayName(car);

        if (!carDisplayNames.TryGetValue(car, out string displayName))
        {
            RegisterAIName(car);
            carDisplayNames.TryGetValue(car, out displayName);
        }

        return string.IsNullOrWhiteSpace(displayName) ? GetAIModelName(car) + " 1" : displayName;
    }

    private void RecordPlayerLapTime(int lapNumber)
    {
        EnsureRaceTimerStarted();
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

    private void SetRaceHudVisible(bool visible)
    {
        if (raceTimeHudText != null)
            raceTimeHudText.gameObject.SetActive(visible);
        if (playerPositionText != null)
            playerPositionText.gameObject.SetActive(visible);
    }

    private string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
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

        SceneTransitionManager.LoadScene(mainMenuSceneName);
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