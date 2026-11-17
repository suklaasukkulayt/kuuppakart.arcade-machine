using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[System.Serializable]
public class LevelInfo
{
    public string displayName;
    public Sprite image;
    [TextArea(2, 5)] public string description;
    public string sceneName;
}

public class DifficultySelector : MonoBehaviour
{
    private enum Stage { Level, Difficulty, Fov, CameraView }

    [Header("Levels")]
    public LevelInfo[] levels;
    public TMP_Text levelNameText;
    public Image levelImage;
    public TMP_Text levelDescriptionText;
    public TMP_Text normalText;

    [Header("FOV text")]
    public TMP_Text fovText;
    public float fovStep = 5f;
    public float minFov = 40f;
    public float maxFov = 110f;
    public float startingFov = 80f;
    public TMP_Text thirdPersonText;

    [Header("Color settings")]
    public Color selectedColor = Color.yellow;
    public Color unselectedColor = Color.white;
    public Color lockedColor = Color.gray;
    public string gameSceneName = "KKartOG";

    private static readonly Difficulty[] DifficultyOptions = { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard };
    private static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD" };
    private Stage stage = Stage.Level;
    private int levelIndex;
    private int difficultyIndex = 1;
    private bool firstPerson;
    private float currentFov;
    private KuuppaKartControls controls;

    private void Start()
    {
        controls = KuuppaKartControls.EnsureInstance();
        currentFov = startingFov;
        RefreshAll();
    }

    private void Update()
    {
        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();

        int direction = controls.MenuHorizontalDirectionThisFrame;
        if (direction == 0)
            direction = controls.MenuSelectionDirectionThisFrame;
        if (direction != 0)
            Change(direction);
        else if (controls.MenuConfirmPressed)
            Confirm();
        else if (controls.MenuBackPressed)
            Back();
    }

    public void MousePrevious()
    {
        Change(-1);
    }

    public void MouseNext()
    {
        Change(1);
    }

    public void MouseConfirm()
    {
        Confirm();
    }

    public void MouseBack()
    {
        Back();
    }

    private void Change(int direction)
    {
        switch (stage)
        {
            case Stage.Level:
                if (levels == null || levels.Length == 0)
                    return;
                levelIndex = (levelIndex + direction + levels.Length) % levels.Length;
                break;
            case Stage.Difficulty:
                difficultyIndex = (difficultyIndex + direction + DifficultyOptions.Length) % DifficultyOptions.Length;
                break;
            case Stage.Fov:
                currentFov = Mathf.Clamp(currentFov + direction * fovStep, minFov, maxFov);
                break;
            case Stage.CameraView:
                firstPerson = !firstPerson;
                break;
        }
        RefreshAll();
    }

    private void Confirm()
    {
        EnsureDifficultyManager();
        switch (stage)
        {
            case Stage.Level:
                stage = Stage.Difficulty;
                break;
            case Stage.Difficulty:
                DifficultyManager.Instance.SelectedDifficulty = DifficultyOptions[difficultyIndex];
                stage = Stage.Fov;
                break;
            case Stage.Fov:
                DifficultyManager.Instance.SelectedFov = currentFov;
                stage = Stage.CameraView;
                break;
            case Stage.CameraView:
                DifficultyManager.Instance.SelectedDifficulty = DifficultyOptions[difficultyIndex];
                DifficultyManager.Instance.SelectedFov = currentFov;
                DifficultyManager.Instance.FirstPerson = firstPerson;
                StartGame();
                return;
        }
        RefreshAll();
    }

    private void Back()
    {
        if (stage == Stage.Level)
            return;
        stage--;
        RefreshAll();
    }

    private void StartGame()
    {
        string scene = gameSceneName;
        if (levels != null && levels.Length > 0 && levelIndex >= 0 && levelIndex < levels.Length && !string.IsNullOrEmpty(levels[levelIndex].sceneName))
            scene = levels[levelIndex].sceneName;

        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("DifficultySelector: The selected gameplay scene is not included in Build Settings: " + scene);
            return;
        }
        SceneManager.LoadScene(scene);
    }

    private void RefreshAll()
    {
        if (levels != null && levels.Length > 0 && levelIndex >= 0 && levelIndex < levels.Length)
        {
            LevelInfo level = levels[levelIndex];
            if (levelNameText != null) levelNameText.text = level.displayName;
            if (levelDescriptionText != null) levelDescriptionText.text = level.description;
            if (levelImage != null)
            {
                levelImage.sprite = level.image;
                levelImage.enabled = level.image != null;
            }
        }
        StyleText(levelNameText, Stage.Level);

        if (normalText != null) normalText.text = DifficultyNames[difficultyIndex];
        StyleText(normalText, Stage.Difficulty);

        if (fovText != null) fovText.text = $"FOV: {currentFov:F0}";
        StyleText(fovText, Stage.Fov);

        if (thirdPersonText != null) thirdPersonText.text = firstPerson ? "First Person" : "Third Person";
        StyleText(thirdPersonText, Stage.CameraView);
    }

    private void StyleText(TMP_Text text, Stage owner)
    {
        if (text == null)
            return;
        if (stage == owner)
        {
            text.color = selectedColor;
            text.fontStyle = FontStyles.Bold | FontStyles.Underline;
        }
        else if (stage > owner)
        {
            text.color = lockedColor;
            text.fontStyle = FontStyles.Normal;
        }
        else
        {
            text.color = unselectedColor;
            text.fontStyle = FontStyles.Normal;
        }
    }

    private void EnsureDifficultyManager()
    {
        if (DifficultyManager.Instance == null)
        {
            GameObject host = new GameObject("DifficultyManager");
            host.AddComponent<DifficultyManager>();
        }
    }
}
