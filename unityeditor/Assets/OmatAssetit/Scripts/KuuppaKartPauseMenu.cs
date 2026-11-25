using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-900)]
public sealed class KuuppaKartPauseMenu : MonoBehaviour
{
    public enum MenuAction
    {
        Resume = 0,
        Settings = 1,
        Quit = 2,
        Resolution = 3,
        Fullscreen = 4,
        FrameRateLimit = 5,
        VSync = 6,
        GraphicsPreset = 7,
        FpsCounter = 8,
        Back = 9,
        Controls = 10
    }

    public enum ControlsEntryAction
    {
        Profile,
        Device,
        MouseMenuInput,
        Rebind,
        ResetBindings,
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

    [Serializable]
    public sealed class ControlsEntry
    {
        public Button button;
        public Graphic highlightGraphic;
        public TMP_Text label;
        public TMP_Text value;
        public ControlsEntryAction action;
        public KuuppaKartAction binding;
    }

    [Header("Canvas references")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GraphicRaycaster menuRaycaster;
    [SerializeField] private MenuEntry[] mainEntries;
    [SerializeField] private MenuEntry[] settingsEntries;
    [SerializeField] private ControlsEntry[] controlsEntries;

    [Header("Selection colors")]
    [SerializeField] private Color selectedBackgroundColor = new Color(0.05f, 0.42f, 0.61f, 1f);
    [SerializeField] private Color selectedTextColor = new Color(1f, 0.86f, 0.25f, 1f);
    [SerializeField] private Color editingTextColor = new Color(0.25f, 1f, 0.65f, 1f);

    private readonly Dictionary<Graphic, Color> originalGraphicColors = new Dictionary<Graphic, Color>();
    private readonly Dictionary<TMP_Text, Color> originalTextColors = new Dictionary<TMP_Text, Color>();
    private readonly List<Vector2Int> availableResolutions = new List<Vector2Int>();
    private readonly List<GameObject> menuButtonsRoots = new List<GameObject>();
    private KuuppaKartControls controls;
    private ScrollRect controlsScrollRect;
    private Vector2 controlsScrollTarget;
    private bool controlsScrollTargetValid;
    [SerializeField] private float controlsScrollSpeed = 1500f;
    private int mainSelection;
    private int settingsSelection;
    private int controlsSelection;
    private bool menuOpen;
    private bool settingsOpen;
    private bool controlsOpen;
    private bool editingSetting;
    private bool selectedController;
    private float previousTimeScale = 1f;
    private Vector2Int editOriginalResolution;
    private int editOriginalFrameRate;
    private int editOriginalPreset;
    private int resolutionIndex;
    private int frameRateIndex;

    private static readonly string[] PresetNames = { "Potato", "Low", "Balanced", "High" };
    public bool IsOpen => menuOpen;

    private void Awake()
    {
        if (menuRoot == null || mainPanel == null || settingsPanel == null)
        {
            Debug.LogError("KuuppaKartPauseMenu: Menu Root, Main Panel and Settings Panel must be assigned.");
            enabled = false;
            return;
        }

        controls = KuuppaKartControls.EnsureInstance();
        CacheMenuButtons();
        if (controlsPanel != null)
            controlsScrollRect = controlsPanel.GetComponentInChildren<ScrollRect>(true);
        if (menuRaycaster == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                menuRaycaster = parentCanvas.GetComponent<GraphicRaycaster>();
        }

        CacheEntries(mainEntries);
        CacheEntries(settingsEntries);
        CacheControlsEntries(controlsEntries);
        menuRoot.SetActive(false);
        SetMenuButtonsVisible(false);
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
        if (controlsPanel != null)
            controlsPanel.SetActive(false);
        ApplyMouseInput();
        Time.timeScale = 1f;
    }

    private void CacheMenuButtons()
    {
        menuButtonsRoots.Clear();
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject candidate = sceneObjects[i];
            if (candidate == null || (candidate.name != "MenuButtons" && candidate.name != "Menu Buttons") || !candidate.scene.IsValid() || candidate.scene.handle != gameObject.scene.handle || candidate.GetComponent<RectTransform>() == null)
                continue;

            menuButtonsRoots.Add(candidate);
        }
    }

    private void SetMenuButtonsVisible(bool visible)
    {
        for (int i = menuButtonsRoots.Count - 1; i >= 0; i--)
        {
            GameObject root = menuButtonsRoots[i];

            if (root == null)
            {
                menuButtonsRoots.RemoveAt(i);
                continue;
            }

            if (root.activeSelf != visible)
                root.SetActive(visible);
        }
    }

    private void OnDestroy()
    {
        if (menuOpen)
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
    }

    private void LateUpdate()
    {
        ApplyMouseInput();
        if (!menuOpen || !controlsOpen || !controlsScrollTargetValid || controlsScrollRect == null || controlsScrollRect.content == null)
            return;

        RectTransform content = controlsScrollRect.content;
        Vector2 next = Vector2.MoveTowards(content.anchoredPosition, controlsScrollTarget, controlsScrollSpeed * Time.unscaledDeltaTime);
        content.anchoredPosition = next;
        if ((next - controlsScrollTarget).sqrMagnitude <= 0.01f)
        {
            content.anchoredPosition = controlsScrollTarget;
            controlsScrollTargetValid = false;
        }
    }

    private void Update()
    {
        ApplyMouseInput();
        if (!GameManager.IsGameplayScene(SceneManager.GetActiveScene().name))
            return;

        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();

        if (controls.PausePressed && !menuOpen)
        {
            OpenMenu();
            return;
        }

        if (!menuOpen)
            return;

        if (controls.IsRebinding)
            return;

        if (controls.MenuBackPressed)
        {
            if (editingSetting)
                CancelEditing();
            else if (settingsOpen || controlsOpen)
                ShowMainMenu();
            else
                ResumeGame();
            return;
        }

        int selectionDirection = controls.MenuSelectionDirectionThisFrame;
        int horizontalDirection = controls.MenuHorizontalDirectionThisFrame;

        if (settingsOpen && editingSetting)
        {
            int direction = controls.Profile == KuuppaKartControlProfile.Arcade ? selectionDirection : horizontalDirection;
            if (direction != 0)
                AdjustEditing(direction);
        }
        else if (controlsOpen)
        {
            ControlsEntry selected = GetSelectedControlsEntry();
            if (controls.Profile != KuuppaKartControlProfile.Arcade && selected != null
                && (selected.action == ControlsEntryAction.Profile || selected.action == ControlsEntryAction.Device)
                && horizontalDirection != 0)
                AdjustControlsValue(selected, horizontalDirection);
            else if (selectionDirection != 0)
                MoveControlsSelection(selectionDirection);
        }
        else if (selectionDirection != 0)
        {
            MoveSelection(selectionDirection);
        }

        if (controls.MenuConfirmPressed)
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
                int index = i;
                entry.button.onClick.AddListener(() => HandleMenuClick(index));
                RegisterPointerEnter(entry.button, () => HandleMenuHover(index));
            }

            CacheGraphicAndText(entry.highlightGraphic, entry.label, entry.value);
        }
    }

    private void CacheControlsEntries(ControlsEntry[] entries)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            ControlsEntry entry = entries[i];
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
                int index = i;
                entry.button.onClick.AddListener(() => HandleControlsClick(index));
                RegisterPointerEnter(entry.button, () => HandleControlsHover(index));
            }

            CacheGraphicAndText(entry.highlightGraphic, entry.label, entry.value);
        }
    }

    private void RegisterPointerEnter(Button button, Action callback)
    {
        if (button == null)
            return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();

        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener(_ => callback());

        trigger.triggers.Add(entry);
    }

    private bool PointerMenuInputEnabled => controls != null && (controls.MouseMenuInputEnabled || controls.TouchInputMode);

    private void HandleMenuHover(int index)
    {
        if (!menuOpen || controlsOpen || !PointerMenuInputEnabled)
            return;

        if (settingsOpen)
        {
            if (settingsSelection != index)
                editingSetting = false;

            settingsSelection = index;
        }
        else
        {
            mainSelection = index;
        }

        RefreshHighlights();
    }

    private void HandleControlsHover(int index)
    {
        if (!menuOpen || !controlsOpen || !PointerMenuInputEnabled)
            return;

        controlsSelection = index;

        RefreshHighlights();
    }

    private void CacheGraphicAndText(Graphic highlight, TMP_Text label, TMP_Text value)
    {
        if (highlight != null && !originalGraphicColors.ContainsKey(highlight))
            originalGraphicColors.Add(highlight, highlight.color);
        if (label != null && !originalTextColors.ContainsKey(label))
            originalTextColors.Add(label, label.color);
        if (value != null && !originalTextColors.ContainsKey(value))
            originalTextColors.Add(value, value.color);
    }

    private void OpenMenu()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        menuOpen = true;
        settingsOpen = false;
        controlsOpen = false;
        editingSetting = false;
        mainSelection = Mathf.Max(0, FindActionIndex(mainEntries, MenuAction.Resume));
        menuRoot.SetActive(true);
        SetMenuButtonsVisible(true);
        ShowMainPanelOnly();
        ApplyMouseInput();
        RefreshHighlights();
    }

    private void ResumeGame()
    {
        menuRoot.SetActive(false);
        SetMenuButtonsVisible(false);
        menuOpen = false;
        settingsOpen = false;
        controlsOpen = false;
        editingSetting = false;
        ApplyMouseInput();
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
    }

    private void ShowMainMenu()
    {
        bool returnedFromControls = controlsOpen;
        editingSetting = false;
        settingsOpen = false;
        controlsOpen = false;
        settingsPanel.SetActive(false);
        if (controlsPanel != null)
            controlsPanel.SetActive(false);
        mainPanel.SetActive(true);
        int target = FindActionIndex(mainEntries, returnedFromControls ? MenuAction.Controls : MenuAction.Settings);
        if (target >= 0)
            mainSelection = target;
        RefreshHighlights();
    }

    private void ShowMainPanelOnly()
    {
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
        if (controlsPanel != null)
            controlsPanel.SetActive(false);
    }

    private void ShowSettings()
    {
        if (controlsPanel != null)
            controlsPanel.SetActive(false);
        settingsOpen = true;
        controlsOpen = false;
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

    private void ShowControls()
    {
        if (controlsPanel == null)
        {
            Debug.LogError("KuuppaKartPauseMenu: Assign a Controls Panel in the Inspector.");
            return;
        }

        settingsOpen = false;
        controlsOpen = true;
        editingSetting = false;
        mainPanel.SetActive(false);
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(true);
        controlsSelection = Mathf.Max(0, FindControlsEntryIndex(ControlsEntryAction.Profile));
        RefreshControlsValues();
        RefreshHighlights();
        ScrollControlsSelectionIntoView(true);
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
        controlsOpen = false;
        editingSetting = false;
        menuRoot.SetActive(false);
        SetMenuButtonsVisible(false);
        SceneTransitionManager.LoadScene("StartScreen");
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

    private void MoveControlsSelection(int direction)
    {
        if (controlsEntries == null || controlsEntries.Length == 0)
            return;
        controlsSelection = WrapIndex(controlsSelection + direction, controlsEntries.Length);
        RefreshHighlights();
        ScrollControlsSelectionIntoView();
    }

    private void ScrollControlsSelectionIntoView(bool immediate = false)
    {
        if (controlsScrollRect == null || controlsScrollRect.content == null)
            return;

        ControlsEntry entry = GetSelectedControlsEntry();
        if (entry == null || entry.button == null)
            return;

        RectTransform viewport = controlsScrollRect.viewport != null
            ? controlsScrollRect.viewport
            : controlsScrollRect.transform as RectTransform;
        RectTransform content = controlsScrollRect.content;
        RectTransform selected = entry.button.transform as RectTransform;
        if (viewport == null || content == null || selected == null)
            return;

        Canvas.ForceUpdateCanvases();
        Vector3[] corners = new Vector3[4];
        selected.GetWorldCorners(corners);
        float itemBottom = float.PositiveInfinity;
        float itemTop = float.NegativeInfinity;
        for (int i = 0; i < corners.Length; i++)
        {
            float y = viewport.InverseTransformPoint(corners[i]).y;
            itemBottom = Mathf.Min(itemBottom, y);
            itemTop = Mathf.Max(itemTop, y);
        }

        Rect viewportRect = viewport.rect;
        float deltaY = 0f;
        if (itemTop > viewportRect.yMax)
            deltaY = viewportRect.yMax - itemTop;
        else if (itemBottom < viewportRect.yMin)
            deltaY = viewportRect.yMin - itemBottom;

        if (Mathf.Abs(deltaY) < 0.5f)
        {
            if (immediate)
            {
                controlsScrollTarget = content.anchoredPosition;
                content.anchoredPosition = controlsScrollTarget;
            }
            controlsScrollTargetValid = false;
            return;
        }

        Vector2 target = content.anchoredPosition + new Vector2(0f, deltaY);
        float maxScroll = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        target.y = Mathf.Clamp(target.y, 0f, maxScroll);
        controlsScrollTarget = target;
        controlsScrollTargetValid = true;
        controlsScrollRect.velocity = Vector2.zero;

        if (immediate)
        {
            content.anchoredPosition = controlsScrollTarget;
            controlsScrollTargetValid = false;
            Canvas.ForceUpdateCanvases();
        }
    }

    private void ConfirmSelection()
    {
        if (controlsOpen)
        {
            ConfirmControlsSelection();
            return;
        }

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
                case MenuAction.Controls:
                    ShowControls();
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

    private void ConfirmControlsSelection()
    {
        ControlsEntry entry = GetSelectedControlsEntry();
        if (entry == null)
            return;

        switch (entry.action)
        {
            case ControlsEntryAction.Profile:
                int nextProfile = ((int)controls.Profile + 1) % 3;
                controls.SetProfile((KuuppaKartControlProfile)nextProfile);
                break;
            case ControlsEntryAction.Device:
                selectedController = !selectedController;
                break;
            case ControlsEntryAction.MouseMenuInput:
                controls.SetMouseMenuInput(!controls.MouseMenuInputEnabled);
                ApplyMouseInput();
                break;
            case ControlsEntryAction.Rebind:
                controls.BeginRebind(entry.binding, selectedController, RefreshControlsValues);
                if (entry.value != null)
                    entry.value.text = "Press a control...";
                break;
            case ControlsEntryAction.ResetBindings:
                controls.ResetCustomBindings();
                break;
            case ControlsEntryAction.Back:
                ShowMainMenu();
                return;
        }

        RefreshControlsValues();
        RefreshHighlights();
    }

    private void AdjustControlsValue(ControlsEntry entry, int direction)
    {
        if (entry.action == ControlsEntryAction.Profile)
        {
            int profile = ((int)controls.Profile + direction + 3) % 3;
            controls.SetProfile((KuuppaKartControlProfile)profile);
        }
        else if (entry.action == ControlsEntryAction.Device)
        {
            selectedController = direction > 0;
        }
        RefreshControlsValues();
        RefreshHighlights();
    }

    private void HandleMenuClick(int index)
    {
        if (!menuOpen || !PointerMenuInputEnabled)
            return;
        if (settingsOpen)
            settingsSelection = index;
        else
            mainSelection = index;
        ConfirmSelection();
    }

    private void HandleControlsClick(int index)
    {
        if (!menuOpen || !PointerMenuInputEnabled)
            return;
        controlsSelection = index;
        ConfirmControlsSelection();
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

        Vector2Int current = GetCurrentResolution();
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
        resolutionIndex = availableResolutions.IndexOf(GetCurrentResolution());
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

    private void RefreshControlsValues()
    {
        if (controlsEntries == null || controls == null)
            return;

        for (int i = 0; i < controlsEntries.Length; i++)
        {
            ControlsEntry entry = controlsEntries[i];
            if (entry == null || entry.value == null)
                continue;

            switch (entry.action)
            {
                case ControlsEntryAction.Profile:
                    entry.value.text = controls.Profile.ToString().ToUpperInvariant();
                    break;
                case ControlsEntryAction.Device:
                    entry.value.text = selectedController ? "CONTROLLER" : "KEYBOARD";
                    break;
                case ControlsEntryAction.MouseMenuInput:
                    entry.value.text = controls.MouseMenuInputEnabled ? "ON" : "OFF";
                    break;
                case ControlsEntryAction.Rebind:
                    entry.value.text = controls.GetBindingDisplayName(entry.binding, selectedController);
                    break;
                case ControlsEntryAction.ResetBindings:
                case ControlsEntryAction.Back:
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
        RefreshEntryHighlights(mainEntries, !settingsOpen && !controlsOpen && menuOpen, mainSelection);
        RefreshEntryHighlights(settingsEntries, settingsOpen && menuOpen, settingsSelection);
        RefreshControlsHighlights(controlsEntries, controlsOpen && menuOpen, controlsSelection);
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
            ApplyHighlight(entry.highlightGraphic, entry.label, entry.value, visible && i == selectedIndex, editingSetting && i == selectedIndex);
        }
    }

    private void RefreshControlsHighlights(ControlsEntry[] entries, bool visible, int selectedIndex)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            ControlsEntry entry = entries[i];
            if (entry == null)
                continue;
            ApplyHighlight(entry.highlightGraphic, entry.label, entry.value, visible && i == selectedIndex, controls.IsRebinding && i == selectedIndex);
        }
    }

    private void ApplyHighlight(Graphic graphic, TMP_Text label, TMP_Text value, bool selected, bool editing)
    {
        if (graphic != null && originalGraphicColors.TryGetValue(graphic, out Color graphicColor))
            graphic.color = selected ? selectedBackgroundColor : graphicColor;
        if (label != null && originalTextColors.TryGetValue(label, out Color labelColor))
            label.color = editing ? editingTextColor : selected ? selectedTextColor : labelColor;
        if (value != null && originalTextColors.TryGetValue(value, out Color valueColor))
            value.color = editing ? editingTextColor : selected ? selectedTextColor : valueColor;
    }

    private void ApplyMouseInput()
    {
        if (menuRaycaster != null)
            menuRaycaster.enabled = menuOpen && controls != null && (controls.MouseMenuInputEnabled || controls.TouchInputMode);
    }

    private MenuEntry GetSelectedMenuEntry()
    {
        MenuEntry[] entries = settingsOpen ? settingsEntries : mainEntries;
        int index = settingsOpen ? settingsSelection : mainSelection;
        return entries != null && index >= 0 && index < entries.Length ? entries[index] : null;
    }

    private ControlsEntry GetSelectedControlsEntry()
    {
        return controlsEntries != null && controlsSelection >= 0 && controlsSelection < controlsEntries.Length
            ? controlsEntries[controlsSelection]
            : null;
    }

    private int FindActionIndex(MenuEntry[] entries, MenuAction action)
    {
        if (entries == null)
            return -1;
        for (int i = 0; i < entries.Length; i++)
            if (entries[i] != null && entries[i].action == action)
                return i;
        return -1;
    }

    private int FindControlsEntryIndex(ControlsEntryAction action)
    {
        if (controlsEntries == null)
            return -1;
        for (int i = 0; i < controlsEntries.Length; i++)
            if (controlsEntries[i] != null && controlsEntries[i].action == action)
                return i;
        return -1;
    }

    private int WrapIndex(int index, int length)
    {
        if (length <= 0)
            return 0;
        return (index % length + length) % length;
    }
}
