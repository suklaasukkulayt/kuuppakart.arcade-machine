using UnityEngine;
using TMPro;
using System.Collections;

public class PelaajanKierrosTarkastus : MonoBehaviour
{
    public int checkpointCount = 11;
    public TMP_Text lapsText;

    public float missedCheckpointMessageDuration = 2f;

    public float missedCheckpointSuppressionAfterLap = 5f;

    private bool[] visited;
    private int visitedCount;

    private int lastDisplayedLap = 0;
    private int lastDisplayedMaxLap = 3;
    private Coroutine missedCheckpointRoutine;

    void Awake()
    {
        ResetLap();
        UpdateLapsTextDisplay();
    }

    public void UpdateLapsText(int currentLap, int maxLap)
    {
        lastDisplayedLap = currentLap;
        lastDisplayedMaxLap = maxLap;

        if (missedCheckpointRoutine == null)
            UpdateLapsTextDisplay();
    }

    private void UpdateLapsTextDisplay()
    {
        if (lapsText == null)
            return;

        lapsText.text = $"<mark>Lap: {lastDisplayedLap}/{lastDisplayedMaxLap}</mark>";
    }

    public void ShowMissedCheckpointsMessage()
    {
        if (lapsText == null)
            return;

        if (missedCheckpointRoutine != null)
            StopCoroutine(missedCheckpointRoutine);

        missedCheckpointRoutine = StartCoroutine(MissedCheckpointsRoutine());
    }

    private IEnumerator MissedCheckpointsRoutine()
    {
        lapsText.text = "You didn't hit all checkpoints!";
        yield return new WaitForSeconds(missedCheckpointMessageDuration);
        UpdateLapsTextDisplay();
        missedCheckpointRoutine = null;
    }

    public void ResetLap()
    {
        visited = new bool[checkpointCount];
        visitedCount = 0;
    }

    public void MarkVisited(int index)
    {
        if (index < 0 || index >= visited.Length)
            return;

        if (!visited[index])
        {
            visited[index] = true;
            visitedCount++;
        }
    }

    public bool AllVisitedThisLap => visitedCount == checkpointCount;
}