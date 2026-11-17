using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

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
    public bool IsRebinding => activeRebinding != null;

    private const string ProfileKey = "KuuppaKart_ControlProfile";
    private const string MouseKey = "KuuppaKart_MouseMenuInput";
    private readonly Dictionary<KuuppaKartAction, InputAction> actions = new Dictionary<KuuppaKartAction, InputAction>();
    private InputActionRebindingExtensions.RebindingOperation activeRebinding;

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
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        CancelRebind();
        DisposeActions();
        Instance = null;
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

    public float MoveVertical => Read(KuuppaKartAction.Accelerate) - Read(KuuppaKartAction.Brake);
    public float MoveHorizontal => Read(KuuppaKartAction.TurnRight) - Read(KuuppaKartAction.TurnLeft);

    public bool WasPressed(KuuppaKartAction action)
    {
        return actions.TryGetValue(action, out InputAction inputAction) && inputAction.WasPressedThisFrame();
    }

    public bool IsPressed(KuuppaKartAction action)
    {
        return actions.TryGetValue(action, out InputAction inputAction) && inputAction.IsPressed();
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
