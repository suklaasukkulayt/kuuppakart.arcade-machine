using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class CarSelectionMenu : MonoBehaviour
{
    [Header("Car database")]
    [SerializeField] private CarDatabase database;
    [SerializeField] private string defaultGameplaySceneName = "KKartOG";
    [SerializeField] private string difficultySceneName = "DifficultySelect";

    [Header("3D preview")]
    [SerializeField] private Transform previewAnchor;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private RenderTexture previewRenderTexture;
    [SerializeField] private int previewLayer = 30;
    [SerializeField] private Vector3 generatedCameraOffset = new Vector3(0f, 2.2f, -6f);
    [SerializeField] private Vector3 previewLookAtOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private bool autoFramePreview = true;
    [SerializeField, Min(1f)] private float previewFramingPadding = 1.35f;
    [SerializeField, Min(0f)] private float previewRotationSpeed = 25f;
    [SerializeField] private int previewTextureWidth = 1280;
    [SerializeField] private Color previewBackground = new Color(0f, 0f, 0f, 0f);

    [Header("UI text")]
    [SerializeField] private TMP_Text carNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text carPositionText;
    [SerializeField] private float speedometerDisplayMultiplier = 10f;
    [SerializeField] private string speedUnit = "km/h";

    [Header("Stat bars")]
    [SerializeField] private RectTransform statsBarsRoot;
    [SerializeField] private Color categoryColor = new Color(0.15f, 0.68f, 0.95f, 1f);
    [SerializeField] private Color statTextColor = new Color(0.12f, 0.14f, 0.2f, 1f);
    [SerializeField] private Color barBackgroundColor = new Color(0.12f, 0.15f, 0.2f, 0.2f);
    [SerializeField] private Color barFillColor = new Color(0.08f, 0.55f, 0.95f, 1f);
    [SerializeField, Min(10f)] private float statLabelWidth = 118f;
    [SerializeField, Min(50f)] private float statValueWidth = 92f;

    private KuuppaKartControls controls;
    private int selectedIndex;
    private GameObject previewInstance;
    private bool ownsPreviewRenderTexture;

    private void Start()
    {
        if (database == null || database.Count == 0)
        {
            Debug.LogError("CarSelectionMenu: Assign a CarDatabase with at least one car.", this);
            enabled = false;
            return;
        }

        controls = KuuppaKartControls.EnsureInstance();
        SetupPreviewOutput();
        SetupStatsLayout();

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
            if (controls.WasPressed(KuuppaKartAction.MenuLeft))
                PreviousCar();
            else if (controls.WasPressed(KuuppaKartAction.MenuRight))
                NextCar();
            else if (controls.WasPressed(KuuppaKartAction.MenuUp))
                PreviousCar();
            else if (controls.WasPressed(KuuppaKartAction.MenuDown))
                NextCar();
            else if (controls.WasPressed(KuuppaKartAction.MenuConfirm))
                ConfirmSelection();
            else if (controls.WasPressed(KuuppaKartAction.MenuBack))
                BackToDifficulty();
        }

        if (previewInstance != null && previewRotationSpeed != 0f)
            previewInstance.transform.Rotate(Vector3.up, previewRotationSpeed * Time.deltaTime, Space.World);
    }

    private void OnDestroy()
    {
        if (previewCamera != null && previewCamera.targetTexture == previewRenderTexture)
            previewCamera.targetTexture = null;

        if (previewImage != null && previewImage.texture == previewRenderTexture)
            previewImage.texture = null;

        if (ownsPreviewRenderTexture && previewRenderTexture != null)
        {
            previewRenderTexture.Release();
            Destroy(previewRenderTexture);
        }
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

        string gameplayScene = PlayerPrefs.GetString(
            CarSelectionKeys.PendingGameplaySceneKey,
            defaultGameplaySceneName
        );

        if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
        {
            Debug.LogError("CarSelectionMenu: Gameplay scene is not included in Build Settings: " + gameplayScene, this);
            return;
        }

        PlayerPrefs.SetString(CarSelectionKeys.SelectedCarIdKey, selectedCar.StableId);
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
            if (statsText != null && statsBarsRoot == null) statsText.text = string.Empty;
            ClearPreview();
            ClearStatsBars();
            return;
        }

        if (carNameText != null)
            carNameText.text = car.displayName;

        if (descriptionText != null)
            descriptionText.text = car.description;

        if (carPositionText != null)
            carPositionText.text = (selectedIndex + 1) + " / " + database.Count;

        if (statsBarsRoot != null)
            BuildStatsBars(car);
        else if (statsText != null)
            statsText.text = BuildStatsText(car);

        RefreshPreview(car);
    }

    private string BuildStatsText(CarDefinition car)
    {
        return "SPEED\n" +
               "Top speed: " + (car.topSpeed * speedometerDisplayMultiplier).ToString("0") + " " + speedUnit + "\n" +
               "Boost max speed: " + (car.boostMaxSpeed * speedometerDisplayMultiplier).ToString("0") + " " + speedUnit + "\n" +
               "Boost duration: " + car.boostDuration.ToString("0.0") + " s\n" +
               "Boost cooldown: " + car.boostCooldown.ToString("0.0") + " s\n\n" +
               "ACCELERATION\n" +
               "Acceleration: " + car.acceleration.ToString("0.0") + "\n" +
               "Boost acceleration: " + car.boostAcceleration.ToString("0.0") + "\n\n" +
               "DRIFTING\n" +
               "Drift angle: " + car.driftBodyAngle.ToString("0") + " deg\n" +
               "Drift slide: " + car.driftLateralSpeed.ToString("0.0") + "\n" +
               "Drift shove: " + car.driftLateralAcceleration.ToString("0.0") + "\n" +
               "Drift yaw: " + car.driftYawMultiplier.ToString("0.00") + "x\n" +
               "Drift grip: " + car.driftRecoveryGrip.ToString("0.0") + "\n\n" +
               "HANDLING\n" +
               "Steering: " + car.turnSpeed.ToString("0") + " deg/s\n" +
               "Braking: " + car.brakePower.ToString("0.0") + "\n\n" +
               "WEIGHT\n" +
               "Rigidbody mass: " + car.rigidbodyMass.ToString("0.0") + " kg";
    }

    private void SetupStatsLayout()
    {
        if (statsBarsRoot == null)
            return;

        if (statsText != null)
            statsText.gameObject.SetActive(false);

        VerticalLayoutGroup layout = statsBarsRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = statsBarsRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 3f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = statsBarsRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = statsBarsRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void BuildStatsBars(CarDefinition car)
    {
        ClearStatsBars();

        AddCategory("SPEED");
        AddStat("Top speed", SpeedValue(car.topSpeed), RatioToDatabaseMaximum(car.topSpeed, c => c.topSpeed));
        AddStat("Boost max speed", SpeedValue(car.boostMaxSpeed), RatioToDatabaseMaximum(car.boostMaxSpeed, c => c.boostMaxSpeed));
        AddStat("Boost duration", car.boostDuration.ToString("0.0") + " s", RatioToDatabaseMaximum(car.boostDuration, c => c.boostDuration));
        AddStat("Boost cooldown", car.boostCooldown.ToString("0.0") + " s", LowerIsBetterRatio(car.boostCooldown, c => c.boostCooldown));

        AddCategory("ACCELERATION");
        AddStat("Acceleration", car.acceleration.ToString("0.0"), RatioToDatabaseMaximum(car.acceleration, c => c.acceleration));
        AddStat("Boost acceleration", car.boostAcceleration.ToString("0.0"), RatioToDatabaseMaximum(car.boostAcceleration, c => c.boostAcceleration));

        AddCategory("DRIFTING");
        AddStat("Drift angle", car.driftBodyAngle.ToString("0.0") + " deg", RatioToDatabaseMaximum(car.driftBodyAngle, c => c.driftBodyAngle));
        AddStat("Drift slide", car.driftLateralSpeed.ToString("0.0"), RatioToDatabaseMaximum(car.driftLateralSpeed, c => c.driftLateralSpeed));
        AddStat("Drift shove", car.driftLateralAcceleration.ToString("0.0"), RatioToDatabaseMaximum(car.driftLateralAcceleration, c => c.driftLateralAcceleration));
        AddStat("Drift yaw", car.driftYawMultiplier.ToString("0.00") + "x", RatioToDatabaseMaximum(car.driftYawMultiplier, c => c.driftYawMultiplier));
        AddStat("Drift grip", car.driftRecoveryGrip.ToString("0.0"), RatioToDatabaseMaximum(car.driftRecoveryGrip, c => c.driftRecoveryGrip));

        AddCategory("HANDLING");
        AddStat("Steering", car.turnSpeed.ToString("0") + " deg/s", RatioToDatabaseMaximum(car.turnSpeed, c => c.turnSpeed));
        AddStat("Braking", car.brakePower.ToString("0.0"), RatioToDatabaseMaximum(car.brakePower, c => c.brakePower));

        AddCategory("WEIGHT");
        AddStat("Rigidbody mass", car.rigidbodyMass.ToString("0") + " kg", RatioToDatabaseMaximum(car.rigidbodyMass, c => c.rigidbodyMass));
    }

    private string SpeedValue(float value)
    {
        return (value * speedometerDisplayMultiplier).ToString("0") + " " + speedUnit;
    }

    private float RatioToDatabaseMaximum(float value, Func<CarDefinition, float> selector)
    {
        float maximum = 0f;
        for (int i = 0; i < database.Count; i++)
        {
            CarDefinition entry = database.GetCar(i);
            if (entry != null)
                maximum = Mathf.Max(maximum, selector(entry));
        }

        return maximum > 0.0001f ? Mathf.Clamp01(value / maximum) : 1f;
    }

    private float LowerIsBetterRatio(float value, Func<CarDefinition, float> selector)
    {
        float minimum = float.MaxValue;
        for (int i = 0; i < database.Count; i++)
        {
            CarDefinition entry = database.GetCar(i);
            if (entry != null && selector(entry) > 0.0001f)
                minimum = Mathf.Min(minimum, selector(entry));
        }

        if (minimum == float.MaxValue || value <= 0.0001f)
            return 1f;

        return Mathf.Clamp01(minimum / value);
    }

    private void AddCategory(string title)
    {
        GameObject categoryObject = new GameObject(title, typeof(RectTransform), typeof(LayoutElement));
        categoryObject.transform.SetParent(statsBarsRoot, false);

        LayoutElement layout = categoryObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 23f;
        layout.minHeight = 23f;

        TMP_Text text = categoryObject.AddComponent<TextMeshProUGUI>();
        SetTextDefaults(text, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
        text.text = title;
        text.color = categoryColor;
    }

    private void AddStat(string label, string value, float fillAmount)
    {
        GameObject rowObject = new GameObject(label, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObject.transform.SetParent(statsBarsRoot, false);

        LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 22f;
        rowLayout.minHeight = 22f;

        HorizontalLayoutGroup row = rowObject.GetComponent<HorizontalLayoutGroup>();
        row.spacing = 7f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = false;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        TMP_Text labelText = CreateText(rowObject.transform, "Label", 14f, FontStyles.Normal, TextAlignmentOptions.Left);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = statLabelWidth;
        labelLayout.minWidth = statLabelWidth;

        GameObject barObject = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        barObject.transform.SetParent(rowObject.transform, false);
        LayoutElement barLayout = barObject.GetComponent<LayoutElement>();
        barLayout.flexibleWidth = 1f;
        barLayout.minWidth = 55f;
        barLayout.preferredHeight = 11f;

        Image background = barObject.GetComponent<Image>();
        background.color = barBackgroundColor;
        background.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(barObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fill = fillObject.GetComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = Mathf.Clamp01(fillAmount);
        fill.color = barFillColor;
        fill.raycastTarget = false;

        TMP_Text valueText = CreateText(rowObject.transform, "Value", 14f, FontStyles.Normal, TextAlignmentOptions.Right);
        LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = statValueWidth;
        valueLayout.minWidth = statValueWidth;
    }

    private TMP_Text CreateText(Transform parent, string objectName, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        SetTextDefaults(text, fontSize, style, alignment);
        return text;
    }

    private void SetTextDefaults(TMP_Text text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        if (carNameText != null && carNameText.font != null)
            text.font = carNameText.font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = statTextColor;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void ClearStatsBars()
    {
        if (statsBarsRoot == null)
            return;

        for (int i = statsBarsRoot.childCount - 1; i >= 0; i--)
            Destroy(statsBarsRoot.GetChild(i).gameObject);
    }

    private void SetupPreviewOutput()
    {
        if (previewAnchor == null)
        {
            Debug.LogWarning("CarSelectionMenu: Assign Preview Anchor. The 3D car preview cannot be created without it.", this);
            return;
        }

        if (previewCamera == null)
        {
            GameObject cameraObject = new GameObject("CarPreviewCamera");
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.fieldOfView = 32f;
            previewCamera.transform.position = previewAnchor.position + generatedCameraOffset;
            previewCamera.transform.LookAt(previewAnchor.position + previewLookAtOffset);
        }

        if (previewImage == null)
        {
            Debug.LogWarning("CarSelectionMenu: Assign a UI RawImage to show the 3D preview inside the Canvas.", this);
            return;
        }

        if (previewRenderTexture == null)
        {
            int width = Mathf.Clamp(previewTextureWidth, 256, 2048);
            Rect imageRect = previewImage.rectTransform.rect;
            float aspect = imageRect.width > 1f && imageRect.height > 1f
                ? imageRect.width / imageRect.height
                : 16f / 9f;
            int height = Mathf.Clamp(Mathf.RoundToInt(width / aspect), 256, 2048);
            previewRenderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            previewRenderTexture.name = "CarSelectionPreviewRuntime";
            previewRenderTexture.filterMode = FilterMode.Bilinear;
            previewRenderTexture.Create();
            ownsPreviewRenderTexture = true;
        }

        previewCamera.targetTexture = previewRenderTexture;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = previewBackground;
        previewCamera.cullingMask = 1 << Mathf.Clamp(previewLayer, 0, 31);
        previewCamera.aspect = (float)previewRenderTexture.width / previewRenderTexture.height;
        previewCamera.nearClipPlane = 0.05f;
        previewCamera.farClipPlane = 500f;
        previewCamera.useOcclusionCulling = false;
        previewCamera.enabled = true;

        previewImage.texture = previewRenderTexture;
        previewImage.color = Color.white;

        Camera mainCamera = Camera.main;
        int previewMask = 1 << Mathf.Clamp(previewLayer, 0, 31);
        if (mainCamera != null && mainCamera != previewCamera)
            mainCamera.cullingMask &= ~previewMask;
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
        SetLayerRecursively(previewInstance.transform, Mathf.Clamp(previewLayer, 0, 31));
        MakeVisualOnly(previewInstance);

        if (autoFramePreview && previewCamera != null)
            FramePreview(previewInstance);
    }

    private void FramePreview(GameObject carObject)
    {
        Renderer[] renderers = carObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 target = bounds.center + previewLookAtOffset;
        Vector3 direction = previewCamera.transform.position - target;
        if (direction.sqrMagnitude < 0.001f)
            direction = new Vector3(0f, 0.25f, -1f);
        direction.Normalize();

        float verticalHalfAngle = previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * Mathf.Max(0.1f, previewCamera.aspect));
        float verticalDistance = bounds.extents.y / Mathf.Max(0.01f, Mathf.Tan(verticalHalfAngle));
        float horizontalDistance = bounds.extents.x / Mathf.Max(0.01f, Mathf.Tan(horizontalHalfAngle));
        float distance = Mathf.Max(verticalDistance, horizontalDistance, bounds.extents.z) * previewFramingPadding;
        distance = Mathf.Max(distance, 1f);

        previewCamera.transform.position = target + direction * distance;
        previewCamera.transform.LookAt(target);
    }

    private void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
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
