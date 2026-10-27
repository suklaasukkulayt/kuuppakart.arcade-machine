using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class KuuppaKartPauseMenu : MonoBehaviour
{
    public enum MenuAction
    {
        Resume,
        Settings,
        Quit,
        Resolution,
        Fullscreen,
        FrameRateLimit,
        VSync,
        GraphicsPreset,
        FpsCounter,
        Back
    }

    [Serializable]
    public sealed class MenuEntry
    {
        public Button button;
        public Graphic highlightGraphic;
        public TMP_Text label;
        public TMP_Text value;
        public MenuAction action;
    }

    [Header("Canvas references")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GraphicRaycaster menuRaycaster;
    [SerializeField] private MenuEntry[] mainEntries;
    [SerializeField] private MenuEntry[] settingsEntries;

    [Header("Selection colors")]
    [SerializeField] private Color selectedBackgroundColor = new Color(0.05f, 0.42f, 0.61f, 1f);
    [SerializeField] private Color selectedTextColor = new Color(1f, 0.86f, 0.25f, 1f);
    [SerializeField] private Color editingTextColor = new Color(0.25f, 1f, 0.65f, 1f);

    private readonly Dictionary<Graphic, Color> originalGraphicColors = new Dictionary<Graphic, Color>();
    private readonly Dictionary<TMP_Text, Color> originalTextColors = new Dictionary<TMP_Text, Color>();
    private readonly List<Vector2Int> availableResolutions = new List<Vector2Int>();
    private int mainSelection;
    private int settingsSelection;
    private bool menuOpen;
    private bool settingsOpen;
    private bool editingSetting;
    private float previousTimeScale = 1f;
    private Vector2Int editOriginalResolution;
    private int editOriginalFrameRate;
    private int editOriginalPreset;
    private int resolutionIndex;
    private int frameRateIndex;

    private static readonly string[] PresetNames = { "Potato", "Low", "Balanced", "High" };

    private void Awake()
    {
        if (menuRoot == null || mainPanel == null || settingsPanel == null)
        {
            Debug.LogError("KuuppaKartPauseMenu: assign Menu Root, Main Panel and Settings Panel in the Inspector.");
            enabled = false;
            return;
        }

        if (menuRaycaster == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                menuRaycaster = parentCanvas.GetComponent<GraphicRaycaster>();
        }

        if (menuRaycaster != null)
            menuRaycaster.enabled = false;

        CacheEntries(mainEntries);
        CacheEntries(settingsEntries);
        menuRoot.SetActive(false);
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        if (menuOpen)
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
    }

    private void Update()
    {
        if (!string.Equals(SceneManager.GetActiveScene().name, "Game", StringComparison.Ordinal))
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!menuOpen)
                OpenMenu();
            else if (editingSetting)
                CancelEditing();
            else if (settingsOpen)
                ShowMainMenu();
            else
                ResumeGame();
            return;
        }

        if (!menuOpen)
            return;

        if (Input.GetKeyDown(KeyCode.S))
        {
            GoBack();
            return;
        }

        int direction = Input.GetKeyDown(KeyCode.A) ? -1 : Input.GetKeyDown(KeyCode.D) ? 1 : 0;
        if (direction != 0)
        {
            if (settingsOpen && editingSetting)
                AdjustEditing(direction);
            else
                MoveSelection(direction);
            return;
        }

        if (Input.GetKeyDown(KeyCode.W))
            ConfirmSelection();
    }

    private void CacheEntries(MenuEntry[] entries)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            MenuEntry entry = entries[i];
            if (entry == null)
                continue;

            if (entry.button != null)
            {
                entry.button.navigation = new Navigation { mode = Navigation.Mode.None };
                entry.button.transition = Selectable.Transition.None;
                if (entry.highlightGraphic == null)
                    entry.highlightGraphic = entry.button.targetGraphic;
                if (entry.label == null)
                    entry.label = entry.button.GetComponentInChildren<TMP_Text>();
            }

            if (entry.highlightGraphic != null && !originalGraphicColors.ContainsKey(entry.highlightGraphic))
                originalGraphicColors.Add(entry.highlightGraphic, entry.highlightGraphic.color);
            if (entry.label != null && !originalTextColors.ContainsKey(entry.label))
                originalTextColors.Add(entry.label, entry.label.color);
            if (entry.value != null && !originalTextColors.ContainsKey(entry.value))
                originalTextColors.Add(entry.value, entry.value.color);
        }
    }

    private void OpenMenu()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        menuOpen = true;
        settingsOpen = false;
        editingSetting = false;
        mainSelection = FindActionIndex(mainEntries, MenuAction.Resume);
        if (mainSelection < 0)
            mainSelection = 0;
        menuRoot.SetActive(true);
        ShowMainPanelOnly();
        RefreshHighlights();
    }

    private void ResumeGame()
    {
        menuRoot.SetActive(false);
        menuOpen = false;
        settingsOpen = false;
        editingSetting = false;
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
    }

    private void ShowMainMenu()
    {
        editingSetting = false;
        settingsOpen = false;
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
        int settingsIndex = FindActionIndex(mainEntries, MenuAction.Settings);
        if (settingsIndex >= 0)
            mainSelection = settingsIndex;
        RefreshHighlights();
    }

    private void ShowMainPanelOnly()
    {
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    private void ShowSettings()
    {
        settingsOpen = true;
        editingSetting = false;
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
        BuildAvailableResolutions();
        SetResolutionIndexToCurrent();
        SetFrameRateIndexToCurrent();
        settingsSelection = Mathf.Max(0, FindActionIndex(settingsEntries, MenuAction.Resolution));
        RefreshSettingsValues();
        RefreshHighlights();
    }

    private void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        if (!Application.CanStreamedLevelBeLoaded("StartScreen"))
        {
            Debug.LogError("KuuppaKartPauseMenu: StartScreen is not included in Build Settings.");
            return;
        }

        menuOpen = false;
        settingsOpen = false;
        editingSetting = false;
        menuRoot.SetActive(false);
        SceneManager.LoadScene("StartScreen");
    }

    private void GoBack()
    {
        if (settingsOpen)
        {
            if (editingSetting)
                CancelEditing();
            else
                ShowMainMenu();
        }
        else
        {
            ResumeGame();
        }
    }

    private void MoveSelection(int direction)
    {
        MenuEntry[] entries = settingsOpen ? settingsEntries : mainEntries;
        if (entries == null || entries.Length == 0)
            return;

        if (settingsOpen)
            settingsSelection = WrapIndex(settingsSelection + direction, entries.Length);
        else
            mainSelection = WrapIndex(mainSelection + direction, entries.Length);

        RefreshHighlights();
    }

    private void ConfirmSelection()
    {
        MenuEntry[] entries = settingsOpen ? settingsEntries : mainEntries;
        if (entries == null || entries.Length == 0)
            return;

        int index = settingsOpen ? settingsSelection : mainSelection;
        if (index < 0 || index >= entries.Length || entries[index] == null)
            return;

        MenuEntry entry = entries[index];
        if (!settingsOpen)
        {
            switch (entry.action)
            {
                case MenuAction.Resume:
                    ResumeGame();
                    break;
                case MenuAction.Settings:
                    ShowSettings();
                    break;
                case MenuAction.Quit:
                    QuitToMainMenu();
                    break;
            }
            return;
        }

        switch (entry.action)
        {
            case MenuAction.Resolution:
            case MenuAction.FrameRateLimit:
            case MenuAction.GraphicsPreset:
                if (editingSetting)
                    editingSetting = false;
                else
                    BeginEditing(entry.action);
                break;
            case MenuAction.Fullscreen:
                KuuppaKartGraphicsSettings.SetFullscreen(!KuuppaKartGraphicsSettings.FullscreenEnabled);
                break;
            case MenuAction.VSync:
                KuuppaKartGraphicsSettings.SetVSync(!KuuppaKartGraphicsSettings.VSyncEnabled);
                break;
            case MenuAction.FpsCounter:
                KuuppaKartGraphicsSettings.SetFpsCounterEnabled(!KuuppaKartGraphicsSettings.FpsCounterEnabled);
                break;
            case MenuAction.Back:
                ShowMainMenu();
                return;
        }

        RefreshSettingsValues();
        RefreshHighlights();
    }

    private void BeginEditing(MenuAction action)
    {
        editingSetting = true;
        editOriginalResolution = new Vector2Int(
            KuuppaKartGraphicsSettings.SavedResolutionWidth > 0 ? KuuppaKartGraphicsSettings.SavedResolutionWidth : Screen.width,
            KuuppaKartGraphicsSettings.SavedResolutionHeight > 0 ? KuuppaKartGraphicsSettings.SavedResolutionHeight : Screen.height);
        editOriginalFrameRate = KuuppaKartGraphicsSettings.FrameRateLimit;
        editOriginalPreset = KuuppaKartGraphicsSettings.CurrentPreset;

        if (action == MenuAction.Resolution)
            SetResolutionIndexToCurrent();
        else if (action == MenuAction.FrameRateLimit)
            SetFrameRateIndexToCurrent();

        RefreshSettingsValues();
        RefreshHighlights();
    }

    private void AdjustEditing(int direction)
    {
        MenuEntry[] entries = settingsEntries;
        if (entries == null || settingsSelection < 0 || settingsSelection >= entries.Length)
            return;

        MenuEntry entry = entries[settingsSelection];
        if (entry == null)
            return;

        switch (entry.action)
        {
            case MenuAction.Resolution:
                if (availableResolutions.Count > 0)
                {
                    resolutionIndex = WrapIndex(resolutionIndex + direction, availableResolutions.Count);
                    Vector2Int resolution = availableResolutions[resolutionIndex];
                    KuuppaKartGraphicsSettings.SetResolution(resolution.x, resolution.y);
                }
                break;
            case MenuAction.FrameRateLimit:
                int[] frameRates = KuuppaKartGraphicsSettings.GetFrameRateOptions();
                frameRateIndex = WrapIndex(frameRateIndex + direction, frameRates.Length);
                KuuppaKartGraphicsSettings.SetFrameRateLimit(frameRates[frameRateIndex]);
                break;
            case MenuAction.GraphicsPreset:
                int preset = WrapIndex(KuuppaKartGraphicsSettings.CurrentPreset + direction, PresetNames.Length);
                KuuppaKartGraphicsSettings.SetPreset(preset);
                break;
        }

        RefreshSettingsValues();
        RefreshHighlights();
    }

    private void CancelEditing()
    {
        if (!editingSetting)
            return;

        MenuEntry entry = settingsEntries != null && settingsSelection >= 0 && settingsSelection < settingsEntries.Length
            ? settingsEntries[settingsSelection]
            : null;

        if (entry != null)
        {
            switch (entry.action)
            {
                case MenuAction.Resolution:
                    KuuppaKartGraphicsSettings.SetResolution(editOriginalResolution.x, editOriginalResolution.y);
                    SetResolutionIndexToCurrent();
                    break;
                case MenuAction.FrameRateLimit:
                    KuuppaKartGraphicsSettings.SetFrameRateLimit(editOriginalFrameRate);
                    SetFrameRateIndexToCurrent();
                    break;
                case MenuAction.GraphicsPreset:
                    KuuppaKartGraphicsSettings.SetPreset(editOriginalPreset);
                    break;
            }
        }

        editingSetting = false;
        RefreshSettingsValues();
        RefreshHighlights();
    }

    private void BuildAvailableResolutions()
    {
        availableResolutions.Clear();
        Resolution[] supported = Screen.resolutions;
        for (int i = 0; i < supported.Length; i++)
        {
            Vector2Int resolution = new Vector2Int(supported[i].width, supported[i].height);
            if (resolution.x >= 640 && resolution.y >= 480 && !availableResolutions.Contains(resolution))
                availableResolutions.Add(resolution);
        }

        Vector2Int current = new Vector2Int(
            KuuppaKartGraphicsSettings.SavedResolutionWidth > 0 ? KuuppaKartGraphicsSettings.SavedResolutionWidth : Screen.width,
            KuuppaKartGraphicsSettings.SavedResolutionHeight > 0 ? KuuppaKartGraphicsSettings.SavedResolutionHeight : Screen.height);
        if (!availableResolutions.Contains(current))
            availableResolutions.Add(current);

        availableResolutions.Sort((a, b) =>
        {
            int areaCompare = (a.x * a.y).CompareTo(b.x * b.y);
            return areaCompare != 0 ? areaCompare : a.x.CompareTo(b.x);
        });
    }

    private void SetResolutionIndexToCurrent()
    {
        Vector2Int current = new Vector2Int(
            KuuppaKartGraphicsSettings.SavedResolutionWidth > 0 ? KuuppaKartGraphicsSettings.SavedResolutionWidth : Screen.width,
            KuuppaKartGraphicsSettings.SavedResolutionHeight > 0 ? KuuppaKartGraphicsSettings.SavedResolutionHeight : Screen.height);
        resolutionIndex = availableResolutions.IndexOf(current);
        if (resolutionIndex < 0)
            resolutionIndex = 0;
    }

    private void SetFrameRateIndexToCurrent()
    {
        int[] frameRates = KuuppaKartGraphicsSettings.GetFrameRateOptions();
        frameRateIndex = Array.IndexOf(frameRates, KuuppaKartGraphicsSettings.FrameRateLimit);
        if (frameRateIndex < 0)
            frameRateIndex = Array.IndexOf(frameRates, 60);
        if (frameRateIndex < 0)
            frameRateIndex = 0;
    }

    private void RefreshSettingsValues()
    {
        if (settingsEntries == null)
            return;

        for (int i = 0; i < settingsEntries.Length; i++)
        {
            MenuEntry entry = settingsEntries[i];
            if (entry == null || entry.value == null)
                continue;

            switch (entry.action)
            {
                case MenuAction.Resolution:
                    Vector2Int resolution = GetCurrentResolution();
                    entry.value.text = resolution.x + " x " + resolution.y;
                    break;
                case MenuAction.Fullscreen:
                    entry.value.text = KuuppaKartGraphicsSettings.FullscreenEnabled ? "ON" : "OFF";
                    break;
                case MenuAction.FrameRateLimit:
                    entry.value.text = KuuppaKartGraphicsSettings.FormatFrameRate(KuuppaKartGraphicsSettings.FrameRateLimit);
                    break;
                case MenuAction.VSync:
                    entry.value.text = KuuppaKartGraphicsSettings.VSyncEnabled ? "ON" : "OFF";
                    break;
                case MenuAction.GraphicsPreset:
                    entry.value.text = PresetNames[KuuppaKartGraphicsSettings.CurrentPreset];
                    break;
                case MenuAction.FpsCounter:
                    entry.value.text = KuuppaKartGraphicsSettings.FpsCounterEnabled ? "ON" : "OFF";
                    break;
                default:
                    entry.value.text = string.Empty;
                    break;
            }
        }
    }

    private Vector2Int GetCurrentResolution()
    {
        int width = KuuppaKartGraphicsSettings.SavedResolutionWidth;
        int height = KuuppaKartGraphicsSettings.SavedResolutionHeight;
        if (width <= 0 || height <= 0)
            return new Vector2Int(Screen.width, Screen.height);
        return new Vector2Int(width, height);
    }

    private void RefreshHighlights()
    {
        RefreshEntryHighlights(mainEntries, !settingsOpen && menuOpen, mainSelection);
        RefreshEntryHighlights(settingsEntries, settingsOpen && menuOpen, settingsSelection);
    }

    private void RefreshEntryHighlights(MenuEntry[] entries, bool visible, int selectedIndex)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            MenuEntry entry = entries[i];
            if (entry == null)
                continue;

            bool selected = visible && i == selectedIndex;
            bool editing = selected && editingSetting;

            if (entry.highlightGraphic != null && originalGraphicColors.TryGetValue(entry.highlightGraphic, out Color originalGraphicColor))
                entry.highlightGraphic.color = selected ? selectedBackgroundColor : originalGraphicColor;

            if (entry.label != null && originalTextColors.TryGetValue(entry.label, out Color originalLabelColor))
                entry.label.color = editing ? editingTextColor : selected ? selectedTextColor : originalLabelColor;

            if (entry.value != null && originalTextColors.TryGetValue(entry.value, out Color originalValueColor))
                entry.value.color = editing ? editingTextColor : selected ? selectedTextColor : originalValueColor;
        }
    }

    private int FindActionIndex(MenuEntry[] entries, MenuAction action)
    {
        if (entries == null)
            return -1;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].action == action)
                return i;
        }

        return -1;
    }

    private int WrapIndex(int index, int length)
    {
        if (length <= 0)
            return 0;
        return (index % length + length) % length;
    }
}
