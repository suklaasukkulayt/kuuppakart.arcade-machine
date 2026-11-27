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

    public int TotalCheckpointCount => visited != null ? visited.Length : Mathf.Max(0, checkpointCount);

    public int SequentialCheckpointCount
    {
        get
        {
            if (visited == null)
                return 0;

            int count = 0;
            while (count < visited.Length && visited[count])
                count++;
            return count;
        }
    }

    private void Awake()
    {
        ResetLap();
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

        lapsText.text = $"Lap: {lastDisplayedLap}/{lastDisplayedMaxLap}";
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
        lapsText.text = "<mark=#ff000050>You didn't hit all checkpoints!</mark>";
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