using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class DifficultySelector : MonoBehaviour
{
    private enum Stage { Difficulty, Fov, CameraView }

    [Header("Difficulty texts")]
    public TMP_Text easyText;
    public TMP_Text normalText;
    public TMP_Text hardText;

    [Header("FOV text")]
    public TMP_Text fovText;
    public float fovStep = 5f;
    public float minFov = 40f;
    public float maxFov = 110f;
    public float startingFov = 80f;

    [Header("Camera view texts")]
    public TMP_Text thirdPersonText;
    public TMP_Text firstPersonText;

    [Header("Color settings")]
    public Color selectedColor = Color.yellow;
    public Color unselectedColor = Color.white;
    public Color lockedColor = Color.gray;

    [Header("Next scene")]
    public string gameSceneName = "Game";

    private readonly Difficulty[] difficultyOptions = { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard };
    private int difficultyIndex = 1;

    private readonly bool[] cameraOptions = { false, true };
    private int cameraIndex = 0;

    private Stage stage = Stage.Difficulty;
    private float currentFov;

    void Start()
    {
        currentFov = startingFov;
        UpdateDifficultyVisuals();
        UpdateFovVisuals();
        UpdateCameraViewVisuals();
    }

    void Update()
    {
        if (stage == Stage.Difficulty)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                difficultyIndex = (difficultyIndex - 1 + difficultyOptions.Length) % difficultyOptions.Length;
                UpdateDifficultyVisuals();
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                difficultyIndex = (difficultyIndex + 1) % difficultyOptions.Length;
                UpdateDifficultyVisuals();
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                ConfirmDifficulty();
            }
        }
        else if (stage == Stage.Fov)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                ChangeFov(-fovStep);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                ChangeFov(fovStep);
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                ConfirmFov();
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                cameraIndex = (cameraIndex - 1 + cameraOptions.Length) % cameraOptions.Length;
                UpdateCameraViewVisuals();
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                cameraIndex = (cameraIndex + 1) % cameraOptions.Length;
                UpdateCameraViewVisuals();
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                ConfirmCameraViewAndStart();
            }
        }
    }

    private void UpdateDifficultyVisuals()
    {
        bool stageActive = stage == Stage.Difficulty;
        SetTextState(easyText, difficultyOptions[difficultyIndex] == Difficulty.Easy, stageActive);
        SetTextState(normalText, difficultyOptions[difficultyIndex] == Difficulty.Normal, stageActive);
        SetTextState(hardText, difficultyOptions[difficultyIndex] == Difficulty.Hard, stageActive);
    }

    private void ConfirmDifficulty()
    {
        EnsureDifficultyManager();
        DifficultyManager.Instance.SelectedDifficulty = difficultyOptions[difficultyIndex];

        stage = Stage.Fov;
        UpdateDifficultyVisuals();
        UpdateFovVisuals();
    }

    private void ChangeFov(float delta)
    {
        currentFov = Mathf.Clamp(currentFov + delta, minFov, maxFov);
        UpdateFovVisuals();
    }

    private void UpdateFovVisuals()
    {
        if (fovText == null) return;

        fovText.text = $"FOV: {currentFov:F0}";

        if (stage == Stage.Fov)
        {
            fovText.color = selectedColor;
            fovText.fontStyle = FontStyles.Bold | FontStyles.Underline;
        }
        else if (stage == Stage.CameraView)
        {
            fovText.color = lockedColor;
            fovText.fontStyle = FontStyles.Normal;
        }
        else
        {
            fovText.color = unselectedColor;
            fovText.fontStyle = FontStyles.Normal;
        }
    }

    private void ConfirmFov()
    {
        EnsureDifficultyManager();
        DifficultyManager.Instance.SelectedFov = currentFov;

        stage = Stage.CameraView;
        UpdateFovVisuals();
        UpdateCameraViewVisuals();
    }


    private void UpdateCameraViewVisuals()
    {
        bool stageActive = stage == Stage.CameraView;
        SetTextState(thirdPersonText, cameraOptions[cameraIndex] == false, stageActive);
        SetTextState(firstPersonText, cameraOptions[cameraIndex] == true, stageActive);
    }

    private void ConfirmCameraViewAndStart()
    {
        EnsureDifficultyManager();
        DifficultyManager.Instance.FirstPerson = cameraOptions[cameraIndex];
        SceneManager.LoadScene(gameSceneName);
    }

    private void SetTextState(TMP_Text text, bool isCurrentSelection, bool stageActive)
    {
        if (text == null) return;

        if (!stageActive)
        {
            text.color = isCurrentSelection ? selectedColor : lockedColor;
            text.fontStyle = isCurrentSelection ? FontStyles.Bold : FontStyles.Normal;
            return;
        }

        text.color = isCurrentSelection ? selectedColor : unselectedColor;
        text.fontStyle = isCurrentSelection ? (FontStyles.Bold | FontStyles.Underline) : FontStyles.Normal;
    }

    private void EnsureDifficultyManager()
    {
        if (DifficultyManager.Instance == null)
        {
            GameObject go = new GameObject("DifficultyManager");
            go.AddComponent<DifficultyManager>();
        }
    }
}