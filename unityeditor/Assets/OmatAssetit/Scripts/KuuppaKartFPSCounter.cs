using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public sealed class KuuppaKartFpsCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text counterText;
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.25f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowFpsColor = Color.red;

    private float elapsed;
    private int frameCount;

    private void Awake()
    {
        if (counterText == null)
            counterText = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (counterText == null)
            return;

        bool enabledBySetting = KuuppaKartGraphicsSettings.FpsCounterEnabled;
        if (counterText.enabled != enabledBySetting)
            counterText.enabled = enabledBySetting;
        if (!enabledBySetting)
            return;

        elapsed += Time.unscaledDeltaTime;
        frameCount++;
        if (elapsed < refreshInterval)
            return;

        float fps = frameCount / Mathf.Max(elapsed, 0.0001f);
        int shownFps = Mathf.RoundToInt(fps);
        int threshold = GetFpsThreshold();
        bool belowTarget = threshold > 0 && fps < threshold;

        counterText.text = shownFps + " FPS";
        counterText.color = belowTarget ? lowFpsColor : normalColor;
        elapsed = 0f;
        frameCount = 0;
    }

    private int GetFpsThreshold()
    {
        if (KuuppaKartGraphicsSettings.VSyncEnabled)
        {
            double refreshRate = Screen.currentResolution.refreshRateRatio.value;
            if (refreshRate > 0.0)
                return Mathf.RoundToInt((float)refreshRate);
            return 60;
        }

        int limit = KuuppaKartGraphicsSettings.FrameRateLimit;
        return limit > 0 ? limit : 0;
    }
}
