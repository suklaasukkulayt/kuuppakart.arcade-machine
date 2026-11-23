using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class CarSelectionMenu : MonoBehaviour
{
    [Header("Car database")]
    [SerializeField] private CarDatabase database;
    [SerializeField] private string defaultGameplaySceneName = "KKartOG";
    [SerializeField] private string difficultySceneName = "DifficultySelect";

    [Header("Preview")]
    [SerializeField] private Transform previewAnchor;
    [SerializeField] private float previewRotationSpeed = 25f;

    [Header("UI text")]
    [SerializeField] private TMP_Text carNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text carPositionText;
    [SerializeField] private float speedometerDisplayMultiplier = 10f;
    [SerializeField] private string speedUnit = "km/h";

    private KuuppaKartControls controls;
    private int selectedIndex;
    private GameObject previewInstance;

    private void Start()
    {
        if (database == null || database.Count == 0)
        {
            Debug.LogError("CarSelectionMenu: Assign a CarDatabase with at least one car.", this);
            enabled = false;
            return;
        }

        controls = KuuppaKartControls.EnsureInstance();
        string savedId = PlayerPrefs.GetString(CarSelectionKeys.SelectedCarIdKey, string.Empty);
        selectedIndex = database.FindIndexById(savedId);
        if (selectedIndex < 0)
            selectedIndex = 0;

        RefreshSelection();
    }

    private void Update()
    {
        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();

        if (controls != null)
        {
            int direction = controls.MenuHorizontalDirectionThisFrame;
            if (direction == 0)
                direction = controls.MenuSelectionDirectionThisFrame;

            if (direction < 0)
            {
                PreviousCar();
                return;
            }

            if (direction > 0)
            {
                NextCar();
                return;
            }

            if (controls.MenuConfirmPressed)
            {
                ConfirmSelection();
                return;
            }

            if (controls.MenuBackPressed)
            {
                BackToDifficulty();
                return;
            }
        }

        if (previewInstance != null && previewRotationSpeed != 0f)
            previewInstance.transform.Rotate(Vector3.up, previewRotationSpeed * Time.deltaTime, Space.World);
    }

    public void PreviousCar()
    {
        ChangeCar(-1);
    }

    public void NextCar()
    {
        ChangeCar(1);
    }

    public void ConfirmSelection()
    {
        CarDefinition selectedCar = database != null ? database.GetCar(selectedIndex) : null;
        if (selectedCar == null)
        {
            Debug.LogError("CarSelectionMenu: The selected car entry is empty.", this);
            return;
        }

        PlayerPrefs.SetString(CarSelectionKeys.SelectedCarIdKey, selectedCar.StableId);

        string gameplayScene = PlayerPrefs.GetString(
            CarSelectionKeys.PendingGameplaySceneKey,
            defaultGameplaySceneName
        );

        if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
        {
            Debug.LogError("CarSelectionMenu: Gameplay scene is not included in Build Settings: " + gameplayScene, this);
            return;
        }

        PlayerPrefs.Save();
        SceneManager.LoadScene(gameplayScene);
    }

    public void BackToDifficulty()
    {
        if (!Application.CanStreamedLevelBeLoaded(difficultySceneName))
        {
            Debug.LogError("CarSelectionMenu: Difficulty scene is not included in Build Settings: " + difficultySceneName, this);
            return;
        }

        SceneManager.LoadScene(difficultySceneName);
    }

    public void SelectCarByIndex(int index)
    {
        if (database == null || database.Count == 0)
            return;

        selectedIndex = Mathf.Clamp(index, 0, database.Count - 1);
        RefreshSelection();
    }

    private void ChangeCar(int direction)
    {
        if (database == null || database.Count == 0)
            return;

        selectedIndex = (selectedIndex + direction + database.Count) % database.Count;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        CarDefinition car = database.GetCar(selectedIndex);
        if (car == null)
        {
            if (carNameText != null) carNameText.text = "MISSING CAR";
            if (descriptionText != null) descriptionText.text = "This entry is empty. Check the Car Database.";
            if (statsText != null) statsText.text = string.Empty;
            ClearPreview();
            return;
        }

        if (carNameText != null)
            carNameText.text = car.displayName;

        if (descriptionText != null)
            descriptionText.text = car.description;

        if (statsText != null)
            statsText.text = BuildStatsText(car);

        if (carPositionText != null)
            carPositionText.text = (selectedIndex + 1) + " / " + database.Count;

        RefreshPreview(car);
    }

    private string BuildStatsText(CarDefinition car)
    {
        return "PERFORMANCE\n" +
               "Acceleration      " + car.acceleration.ToString("0.0") + "\n" +
               "Top speed         " + (car.topSpeed * speedometerDisplayMultiplier).ToString("0") + " " + speedUnit + "\n" +
               "Boost max speed   " + (car.boostMaxSpeed * speedometerDisplayMultiplier).ToString("0") + " " + speedUnit + "\n" +
               "Steering          " + car.turnSpeed.ToString("0") + " deg/s\n" +
               "Braking           " + car.brakePower.ToString("0.0") + "\n\n" +
               "DRIFT & BOOST\n" +
               "Drift angle       " + car.driftBodyAngle.ToString("0") + " deg\n" +
               "Drift slide       " + car.driftLateralSpeed.ToString("0.0") + "\n" +
               "Drift shove       " + car.driftLateralAcceleration.ToString("0.0") + "\n" +
               "Drift yaw         " + car.driftYawMultiplier.ToString("0.00") + "x\n" +
               "Drift grip        " + car.driftRecoveryGrip.ToString("0.0") + "\n" +
               "Boost duration    " + car.boostDuration.ToString("0.0") + " s\n" +
               "Boost cooldown    " + car.boostCooldown.ToString("0.0") + " s\n\n" +
               "Rigidbody mass    " + car.rigidbodyMass.ToString("0.00");
    }

    private void RefreshPreview(CarDefinition car)
    {
        ClearPreview();

        if (previewAnchor == null || car.visualPrefab == null)
            return;

        previewInstance = Instantiate(car.visualPrefab, previewAnchor, false);
        previewInstance.name = car.displayName + " Preview";
        previewInstance.transform.localPosition = car.visualLocalPosition;
        previewInstance.transform.localRotation = Quaternion.Euler(car.visualLocalEulerAngles);
        previewInstance.transform.localScale = car.visualLocalScale;
        MakeVisualOnly(previewInstance);
    }

    private void ClearPreview()
    {
        if (previewInstance == null)
            return;

        Destroy(previewInstance);
        previewInstance = null;
    }

    private void MakeVisualOnly(GameObject carObject)
    {
        Collider[] colliders = carObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody[] bodies = carObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }

        MonoBehaviour[] scripts = carObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < scripts.Length; i++)
            scripts[i].enabled = false;

        Camera[] cameras = carObject.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
            cameras[i].enabled = false;

        AudioListener[] listeners = carObject.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
            listeners[i].enabled = false;
    }
}
