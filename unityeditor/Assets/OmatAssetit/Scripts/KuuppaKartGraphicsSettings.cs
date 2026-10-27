using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class KuuppaKartGraphicsSettings
{
    public enum GraphicsPreset
    {
        Potato = 0,
        Low = 1,
        Balanced = 2,
        High = 3
    }

    private const string PresetKey = "KuuppaKart_GraphicsPreset";
    private const string FrameRateKey = "KuuppaKart_FrameRateLimit";
    private const string VSyncKey = "KuuppaKart_VSync";
    private const string FullscreenKey = "KuuppaKart_Fullscreen";
    private const string ResolutionWidthKey = "KuuppaKart_ResolutionWidth";
    private const string ResolutionHeightKey = "KuuppaKart_ResolutionHeight";
    private const string FpsCounterKey = "KuuppaKart_FpsCounterEnabled";

    private static readonly int[] FrameRateOptions = { 30, 45, 60, 90, 120, -1 };

    public static int CurrentPreset => Mathf.Clamp(PlayerPrefs.GetInt(PresetKey, (int)GraphicsPreset.Potato), 0, 3);
    public static int FrameRateLimit => PlayerPrefs.GetInt(FrameRateKey, 60);
    public static bool VSyncEnabled => PlayerPrefs.GetInt(VSyncKey, 0) != 0;
    public static bool FpsCounterEnabled => PlayerPrefs.GetInt(FpsCounterKey, 0) != 0;
    public static bool FullscreenEnabled => PlayerPrefs.HasKey(FullscreenKey)
        ? PlayerPrefs.GetInt(FullscreenKey, 0) != 0
        : Screen.fullScreen;
    public static int SavedResolutionWidth => PlayerPrefs.GetInt(ResolutionWidthKey, 0);
    public static int SavedResolutionHeight => PlayerPrefs.GetInt(ResolutionHeightKey, 0);

    public static int[] GetFrameRateOptions()
    {
        return (int[])FrameRateOptions.Clone();
    }

    public static string FormatFrameRate(int value)
    {
        return value < 0 ? "Unlimited" : value + " FPS";
    }

    public static void ApplySavedSettingsOnStartup()
    {
        ApplyPresetInternal(CurrentPreset);

        int width = SavedResolutionWidth;
        int height = SavedResolutionHeight;
        bool hasSavedScreenMode = PlayerPrefs.HasKey(FullscreenKey);
        bool hasSavedResolution = width > 0 && height > 0;

        if (hasSavedScreenMode || hasSavedResolution)
        {
            if (!hasSavedResolution)
            {
                width = Mathf.Max(640, Screen.width);
                height = Mathf.Max(480, Screen.height);
            }

            FullScreenMode mode = FullscreenEnabled ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
            Screen.SetResolution(width, height, mode);
        }

        ApplyFrameRateAndVSync();
        QualitySettings.maxQueuedFrames = 2;
    }

    public static void SetPreset(int preset)
    {
        preset = Mathf.Clamp(preset, 0, 3);
        PlayerPrefs.SetInt(PresetKey, preset);
        PlayerPrefs.Save();
        ApplyPresetInternal(preset);
        ApplyFrameRateAndVSync();
    }

    public static void SetFrameRateLimit(int limit)
    {
        bool isValid = false;
        for (int i = 0; i < FrameRateOptions.Length; i++)
        {
            if (FrameRateOptions[i] == limit)
            {
                isValid = true;
                break;
            }
        }

        if (!isValid)
            limit = 60;

        PlayerPrefs.SetInt(FrameRateKey, limit);
        PlayerPrefs.Save();
        ApplyFrameRateAndVSync();
    }

    public static void SetVSync(bool enabled)
    {
        PlayerPrefs.SetInt(VSyncKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyFrameRateAndVSync();
    }

    public static void SetFpsCounterEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(FpsCounterKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetFullscreen(bool enabled)
    {
        PlayerPrefs.SetInt(FullscreenKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        int width = SavedResolutionWidth > 0 ? SavedResolutionWidth : Mathf.Max(640, Screen.width);
        int height = SavedResolutionHeight > 0 ? SavedResolutionHeight : Mathf.Max(480, Screen.height);
        FullScreenMode mode = enabled ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
        Screen.SetResolution(width, height, mode);
    }

    public static void SetResolution(int width, int height)
    {
        width = Mathf.Max(640, width);
        height = Mathf.Max(480, height);
        PlayerPrefs.SetInt(ResolutionWidthKey, width);
        PlayerPrefs.SetInt(ResolutionHeightKey, height);
        PlayerPrefs.Save();

        FullScreenMode mode = FullscreenEnabled ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
        Screen.SetResolution(width, height, mode);
    }

    private static void ApplyFrameRateAndVSync()
    {
        QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
        Application.targetFrameRate = FrameRateLimit;
    }

    private static void ApplyPresetInternal(int preset)
    {
        preset = Mathf.Clamp(preset, 0, 3);
        bool useMobileProfile = preset == (int)GraphicsPreset.Potato || preset == (int)GraphicsPreset.Low;
        string preferredQualityName = useMobileProfile ? "Mobile" : "Desktop";
        int qualityIndex = FindQualityIndex(preferredQualityName);

        if (qualityIndex < 0 && QualitySettings.names.Length > 0)
            qualityIndex = useMobileProfile ? 0 : QualitySettings.names.Length - 1;

        if (qualityIndex >= 0 && QualitySettings.GetQualityLevel() != qualityIndex)
            QualitySettings.SetQualityLevel(qualityIndex, true);

        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        switch ((GraphicsPreset)preset)
        {
            case GraphicsPreset.Potato:
                ConfigureUrp(urp, 0.60f, false, false, false, 1, 0f);
                QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                QualitySettings.shadowDistance = 0f;
                QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;
                QualitySettings.pixelLightCount = 0;
                QualitySettings.lodBias = 0.35f;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                QualitySettings.skinWeights = SkinWeights.TwoBones;
                QualitySettings.softParticles = false;
                QualitySettings.softVegetation = false;
                QualitySettings.realtimeReflectionProbes = false;
                break;
            case GraphicsPreset.Low:
                ConfigureUrp(urp, 0.75f, false, false, false, 1, 12f);
                QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
                QualitySettings.shadowDistance = 12f;
                QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;
                QualitySettings.pixelLightCount = 0;
                QualitySettings.lodBias = 0.55f;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                QualitySettings.skinWeights = SkinWeights.TwoBones;
                QualitySettings.softParticles = false;
                QualitySettings.softVegetation = false;
                QualitySettings.realtimeReflectionProbes = false;
                break;
            case GraphicsPreset.Balanced:
                ConfigureUrp(urp, 0.90f, false, false, false, 1, 25f);
                QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
                QualitySettings.shadowDistance = 25f;
                QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
                QualitySettings.pixelLightCount = 1;
                QualitySettings.lodBias = 1f;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                QualitySettings.skinWeights = SkinWeights.TwoBones;
                QualitySettings.softParticles = false;
                QualitySettings.softVegetation = false;
                QualitySettings.realtimeReflectionProbes = false;
                break;
            case GraphicsPreset.High:
                ConfigureUrp(urp, 1.0f, true, true, true, 2, 50f);
                QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                QualitySettings.shadowDistance = 50f;
                QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
                QualitySettings.pixelLightCount = 2;
                QualitySettings.lodBias = 1.5f;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                QualitySettings.skinWeights = SkinWeights.FourBones;
                QualitySettings.softParticles = true;
                QualitySettings.softVegetation = true;
                QualitySettings.realtimeReflectionProbes = true;
                break;
        }

        QualitySettings.maxQueuedFrames = 2;
    }

    private static void ConfigureUrp(
        UniversalRenderPipelineAsset urp,
        float renderScale,
        bool hdr,
        bool depthTexture,
        bool opaqueTexture,
        int msaaSamples,
        float shadowDistance)
    {
        if (urp == null)
            return;

        urp.renderScale = renderScale;
        urp.supportsHDR = hdr;
        urp.supportsCameraDepthTexture = depthTexture;
        urp.supportsCameraOpaqueTexture = opaqueTexture;
        urp.msaaSampleCount = msaaSamples;
        urp.shadowDistance = shadowDistance;
    }

    private static int FindQualityIndex(string desiredName)
    {
        string[] names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], desiredName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }
}
