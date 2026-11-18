using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

public enum KuuppaKartControlProfile
{
    Normal,
    Arcade,
    Custom
}

public enum KuuppaKartAction
{
    Accelerate,
    Brake,
    TurnLeft,
    TurnRight,
    Boost,
    LookBack,
    Pause,
    Reset,
    Start,
    MenuUp,
    MenuDown,
    MenuLeft,
    MenuRight,
    MenuConfirm,
    MenuBack
}

[DefaultExecutionOrder(-1000)]
public sealed class KuuppaKartControls : MonoBehaviour
{
    [Header("Default Controls")]
    public KuuppaKartControlProfile defaultProfile = KuuppaKartControlProfile.Normal;
    public bool defaultMouseMenuInput = true;

    public static KuuppaKartControls Instance { get; private set; }
    public KuuppaKartControlProfile Profile { get; private set; }
    public bool MouseMenuInputEnabled { get; private set; }
    public bool TouchInputMode { get; private set; }
    public bool IsRebinding => activeRebinding != null;

    private const string ProfileKey = "KuuppaKart_ControlProfile";
    private const string MouseKey = "KuuppaKart_MouseMenuInput";
    private readonly Dictionary<KuuppaKartAction, InputAction> actions = new Dictionary<KuuppaKartAction, InputAction>();
    private readonly HashSet<KuuppaKartAction> onScreenHeld = new HashSet<KuuppaKartAction>();
    private readonly HashSet<KuuppaKartAction> onScreenPressedPending = new HashSet<KuuppaKartAction>();
    private readonly List<GameObject> onScreenButtonRoots = new List<GameObject>();
    private InputActionRebindingExtensions.RebindingOperation activeRebinding;
    private bool onScreenButtonsVisible;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Profile = (KuuppaKartControlProfile)Mathf.Clamp(PlayerPrefs.GetInt(ProfileKey, (int)defaultProfile), 0, 2);
        MouseMenuInputEnabled = PlayerPrefs.GetInt(MouseKey, defaultMouseMenuInput ? 1 : 0) != 0;
        BuildActions();
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void Update()
    {
        DetectInputDeviceForOnScreenButtons();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        CancelRebind();
        DisposeActions();
        Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        onScreenButtonRoots.Clear();
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject candidate = sceneObjects[i];
            if (candidate == null || candidate.name != "Buttons" || !candidate.scene.IsValid() || candidate.scene.handle != scene.handle)
                continue;
            if (candidate.GetComponent<RectTransform>() == null)
                continue;
            onScreenButtonRoots.Add(candidate);
            candidate.SetActive(onScreenButtonsVisible);
        }
    }

    private void DetectInputDeviceForOnScreenButtons()
    {
        bool touchInput = false;
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            touchInput = touchscreen.primaryTouch.press.wasPressedThisFrame
                || touchscreen.primaryTouch.delta.ReadValue().sqrMagnitude > 0.01f;
        }

        bool keyboardInput = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool mouseInput = false;
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            mouseInput = mouse.delta.ReadValue().sqrMagnitude > 0.01f
                || mouse.scroll.ReadValue().sqrMagnitude > 0.01f
                || mouse.leftButton.wasPressedThisFrame
                || mouse.rightButton.wasPressedThisFrame
                || mouse.middleButton.wasPressedThisFrame;
        }

        bool gamepadInput = false;
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            gamepadInput = gamepad.leftStick.ReadValue().sqrMagnitude > 0.04f
                || gamepad.rightStick.ReadValue().sqrMagnitude > 0.04f
                || gamepad.dpad.ReadValue().sqrMagnitude > 0f
                || gamepad.buttonSouth.wasPressedThisFrame
                || gamepad.buttonEast.wasPressedThisFrame
                || gamepad.buttonWest.wasPressedThisFrame
                || gamepad.buttonNorth.wasPressedThisFrame
                || gamepad.leftShoulder.wasPressedThisFrame
                || gamepad.rightShoulder.wasPressedThisFrame
                || gamepad.startButton.wasPressedThisFrame
                || gamepad.selectButton.wasPressedThisFrame
                || gamepad.leftTrigger.ReadValue() > 0.2f
                || gamepad.rightTrigger.ReadValue() > 0.2f;
        }

        if (keyboardInput || gamepadInput)
        {
            TouchInputMode = false;
            SetOnScreenButtonsVisible(false);
        }
        else if (touchInput)
        {
            TouchInputMode = true;
            SetOnScreenButtonsVisible(true);
        }
        else if (mouseInput)
        {
            TouchInputMode = false;
            SetOnScreenButtonsVisible(true);
        }
    }

    private void SetOnScreenButtonsVisible(bool visible)
    {
        if (onScreenButtonsVisible == visible)
            return;

        onScreenButtonsVisible = visible;
        for (int i = onScreenButtonRoots.Count - 1; i >= 0; i--)
        {
            GameObject root = onScreenButtonRoots[i];
            if (root == null)
            {
                onScreenButtonRoots.RemoveAt(i);
                continue;
            }
            if (root.activeSelf != visible)
                root.SetActive(visible);
        }
    }

    public static KuuppaKartControls EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        KuuppaKartControls existing = FindFirstObjectByType<KuuppaKartControls>();
        if (existing != null)
            return existing;

        GameObject host = new GameObject("KuuppaKartControls");
        return host.AddComponent<KuuppaKartControls>();
    }

    public void SetProfile(KuuppaKartControlProfile profile)
    {
        profile = (KuuppaKartControlProfile)Mathf.Clamp((int)profile, 0, 2);
        if (Profile == profile)
            return;

        CancelRebind();
        Profile = profile;
        PlayerPrefs.SetInt(ProfileKey, (int)Profile);
        PlayerPrefs.Save();
        BuildActions();
    }

    public void SetMouseMenuInput(bool enabled)
    {
        MouseMenuInputEnabled = enabled;
        PlayerPrefs.SetInt(MouseKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public float MoveVertical => Mathf.Clamp(Read(KuuppaKartAction.Accelerate) - Read(KuuppaKartAction.Brake) + (onScreenHeld.Contains(KuuppaKartAction.Accelerate) ? 1f : 0f) - (onScreenHeld.Contains(KuuppaKartAction.Brake) ? 1f : 0f), -1f, 1f);
    public float MoveHorizontal => Mathf.Clamp(Read(KuuppaKartAction.TurnRight) - Read(KuuppaKartAction.TurnLeft) + (onScreenHeld.Contains(KuuppaKartAction.TurnRight) ? 1f : 0f) - (onScreenHeld.Contains(KuuppaKartAction.TurnLeft) ? 1f : 0f), -1f, 1f);
    public void SetOnScreenButton(KuuppaKartAction action, bool pressed)
    {
        if (pressed)
        {
            if (onScreenHeld.Add(action))
                onScreenPressedPending.Add(action);
        }
        else
        {
            onScreenHeld.Remove(action);
        }
}

    public bool WasPressed(KuuppaKartAction action)
    {
        bool inputPressed = actions.TryGetValue(action, out InputAction inputAction) && inputAction.WasPressedThisFrame();
        bool touchPressed = onScreenPressedPending.Remove(action);
        return inputPressed || touchPressed;
    }

    public bool IsPressed(KuuppaKartAction action)
    {
        return (actions.TryGetValue(action, out InputAction inputAction) && inputAction.IsPressed()) || onScreenHeld.Contains(action);
    }

    public int MenuSelectionDirectionThisFrame
    {
        get
        {
            if (Profile == KuuppaKartControlProfile.Arcade)
                return WasPressed(KuuppaKartAction.MenuRight) ? 1 : WasPressed(KuuppaKartAction.MenuLeft) ? -1 : 0;
            return WasPressed(KuuppaKartAction.MenuDown) ? 1 : WasPressed(KuuppaKartAction.MenuUp) ? -1 : 0;
        }
    }

    public int MenuHorizontalDirectionThisFrame
    {
        get
        {
            return WasPressed(KuuppaKartAction.MenuRight) ? 1 : WasPressed(KuuppaKartAction.MenuLeft) ? -1 : 0;
        }
    }

    public bool MenuConfirmPressed => WasPressed(KuuppaKartAction.MenuConfirm);
    public bool MenuBackPressed => WasPressed(KuuppaKartAction.MenuBack);
    public bool PausePressed => WasPressed(KuuppaKartAction.Pause);
    public bool ResetPressed => WasPressed(KuuppaKartAction.Reset);
    public bool StartPressed => WasPressed(KuuppaKartAction.Start);
    public bool BoostPressed => WasPressed(KuuppaKartAction.Boost);
    public bool LookBackHeld => IsPressed(KuuppaKartAction.LookBack);

    public string GetBindingDisplayName(KuuppaKartAction action, bool controller)
    {
        string saved = PlayerPrefs.GetString(GetBindingKey(action, controller), string.Empty);
        if (!string.IsNullOrEmpty(saved))
            return InputControlPath.ToHumanReadableString(saved, InputControlPath.HumanReadableStringOptions.OmitDevice);

        KuuppaKartControlProfile sourceProfile = Profile == KuuppaKartControlProfile.Arcade ? KuuppaKartControlProfile.Arcade : KuuppaKartControlProfile.Normal;
        string[] paths = controller ? GetControllerPaths(action) : GetKeyboardPaths(action, sourceProfile);
        return paths.Length > 0
            ? InputControlPath.ToHumanReadableString(paths[0], InputControlPath.HumanReadableStringOptions.OmitDevice)
            : "Not set";
    }

    public void BeginRebind(KuuppaKartAction action, bool controller, Action completed = null)
    {
        CancelRebind();
        if (Profile != KuuppaKartControlProfile.Custom)
            SetProfile(KuuppaKartControlProfile.Custom);

        if (!actions.TryGetValue(action, out InputAction target))
            return;

        target.Disable();
        string devicePath = controller ? "<Gamepad>" : "<Keyboard>";
        string cancelPath = controller ? "<Gamepad>/buttonEast" : "<Keyboard>/escape";
        activeRebinding = target.PerformInteractiveRebinding()
            .WithControlsHavingToMatchPath(devicePath)
            .WithCancelingThrough(cancelPath)
            .OnComplete(operation =>
            {
                string selectedPath = operation.selectedControl != null ? operation.selectedControl.path : string.Empty;
                operation.Dispose();
                activeRebinding = null;
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    PlayerPrefs.SetString(GetBindingKey(action, controller), selectedPath);
                    PlayerPrefs.Save();
                }
                BuildActions();
                completed?.Invoke();
            })
            .OnCancel(operation =>
            {
                operation.Dispose();
                activeRebinding = null;
                BuildActions();
                completed?.Invoke();
            });
        activeRebinding.Start();
    }

    public void CancelRebind()
    {
        if (activeRebinding == null)
            return;

        InputActionRebindingExtensions.RebindingOperation operation = activeRebinding;
        activeRebinding = null;
        operation.Cancel();
    }

    public void ResetCustomBindings()
    {
        foreach (KuuppaKartAction action in Enum.GetValues(typeof(KuuppaKartAction)))
        {
            PlayerPrefs.DeleteKey(GetBindingKey(action, false));
            PlayerPrefs.DeleteKey(GetBindingKey(action, true));
        }
        PlayerPrefs.Save();
        BuildActions();
    }

    private float Read(KuuppaKartAction action)
    {
        return actions.TryGetValue(action, out InputAction inputAction) ? inputAction.ReadValue<float>() : 0f;
    }

    private void BuildActions()
    {
        DisposeActions();
        foreach (KuuppaKartAction action in Enum.GetValues(typeof(KuuppaKartAction)))
        {
            string[] keyboardPaths = Profile == KuuppaKartControlProfile.Custom
                ? GetCustomPaths(action, false)
                : GetKeyboardPaths(action, Profile);
            string[] controllerPaths = Profile == KuuppaKartControlProfile.Custom
                ? GetCustomPaths(action, true)
                : GetControllerPaths(action);

            InputAction inputAction = new InputAction(action.ToString(), InputActionType.Button, expectedControlType: "Button");
            for (int i = 0; i < keyboardPaths.Length; i++)
                inputAction.AddBinding(keyboardPaths[i]);
            for (int i = 0; i < controllerPaths.Length; i++)
                inputAction.AddBinding(controllerPaths[i]);
            inputAction.Enable();
            actions.Add(action, inputAction);
        }
    }

    private string[] GetCustomPaths(KuuppaKartAction action, bool controller)
    {
        string saved = PlayerPrefs.GetString(GetBindingKey(action, controller), string.Empty);
        if (!string.IsNullOrEmpty(saved))
            return new[] { saved };
        return controller ? GetControllerPaths(action) : GetKeyboardPaths(action, KuuppaKartControlProfile.Normal);
    }

    private string GetBindingKey(KuuppaKartAction action, bool controller)
    {
        return "KuuppaKart_Custom_" + action + (controller ? "_Controller" : "_Keyboard");
    }

    private void DisposeActions()
    {
        foreach (InputAction inputAction in actions.Values)
        {
            inputAction.Disable();
            inputAction.Dispose();
        }
        actions.Clear();
    }

    private static string[] GetKeyboardPaths(KuuppaKartAction action, KuuppaKartControlProfile profile)
    {
        switch (action)
        {
            case KuuppaKartAction.Accelerate: return new[] { "<Keyboard>/w", "<Keyboard>/upArrow" };
            case KuuppaKartAction.Brake: return new[] { "<Keyboard>/s", "<Keyboard>/downArrow" };
            case KuuppaKartAction.TurnLeft: return new[] { "<Keyboard>/a", "<Keyboard>/leftArrow" };
            case KuuppaKartAction.TurnRight: return new[] { "<Keyboard>/d", "<Keyboard>/rightArrow" };
            case KuuppaKartAction.Boost: return new[] { "<Keyboard>/b" };
            case KuuppaKartAction.LookBack: return new[] { "<Keyboard>/v" };
            case KuuppaKartAction.Pause: return new[] { "<Keyboard>/escape" };
            case KuuppaKartAction.Reset: return new[] { profile == KuuppaKartControlProfile.Arcade ? "<Keyboard>/enter" : "<Keyboard>/r" };
            case KuuppaKartAction.Start: return new[] { profile == KuuppaKartControlProfile.Arcade ? "<Keyboard>/p" : "<Keyboard>/enter" };
            case KuuppaKartAction.MenuUp: return profile == KuuppaKartControlProfile.Arcade ? Array.Empty<string>() : new[] { "<Keyboard>/upArrow" };
            case KuuppaKartAction.MenuDown: return profile == KuuppaKartControlProfile.Arcade ? Array.Empty<string>() : new[] { "<Keyboard>/downArrow" };
            case KuuppaKartAction.MenuLeft: return new[] { profile == KuuppaKartControlProfile.Arcade ? "<Keyboard>/a" : "<Keyboard>/leftArrow" };
            case KuuppaKartAction.MenuRight: return new[] { profile == KuuppaKartControlProfile.Arcade ? "<Keyboard>/d" : "<Keyboard>/rightArrow" };
            case KuuppaKartAction.MenuConfirm: return new[] { profile == KuuppaKartControlProfile.Arcade ? "<Keyboard>/w" : "<Keyboard>/enter" };
            case KuuppaKartAction.MenuBack: return new[] { profile == KuuppaKartControlProfile.Arcade ? "<Keyboard>/s" : "<Keyboard>/escape" };
            default: return Array.Empty<string>();
        }
    }

    private static string[] GetControllerPaths(KuuppaKartAction action)
    {
        switch (action)
        {
            case KuuppaKartAction.Accelerate: return new[] { "<Gamepad>/leftStick/up", "<Gamepad>/dpad/up" };
            case KuuppaKartAction.Brake: return new[] { "<Gamepad>/leftStick/down", "<Gamepad>/dpad/down" };
            case KuuppaKartAction.TurnLeft: return new[] { "<Gamepad>/leftStick/left", "<Gamepad>/dpad/left" };
            case KuuppaKartAction.TurnRight: return new[] { "<Gamepad>/leftStick/right", "<Gamepad>/dpad/right" };
            case KuuppaKartAction.Boost: return new[] { "<Gamepad>/rightShoulder" };
            case KuuppaKartAction.LookBack: return new[] { "<Gamepad>/buttonNorth" };
            case KuuppaKartAction.Pause: return new[] { "<Gamepad>/start" };
            case KuuppaKartAction.Reset: return new[] { "<Gamepad>/select" };
            case KuuppaKartAction.Start: return new[] { "<Gamepad>/start" };
            case KuuppaKartAction.MenuUp: return new[] { "<Gamepad>/dpad/up" };
            case KuuppaKartAction.MenuDown: return new[] { "<Gamepad>/dpad/down" };
            case KuuppaKartAction.MenuLeft: return new[] { "<Gamepad>/dpad/left" };
            case KuuppaKartAction.MenuRight: return new[] { "<Gamepad>/dpad/right" };
            case KuuppaKartAction.MenuConfirm: return new[] { "<Gamepad>/buttonSouth" };
            case KuuppaKartAction.MenuBack: return new[] { "<Gamepad>/buttonEast" };
            default: return Array.Empty<string>();
        }
    }
}
