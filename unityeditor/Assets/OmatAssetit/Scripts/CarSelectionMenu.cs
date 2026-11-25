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
    [SerializeField] private Vector3 previewCameraLocalDirection = new Vector3(-0.8f, 1f, -1.8f);
    [SerializeField] private bool autoFramePreview = true;
    [SerializeField, Min(1f)] private float previewFramingPadding = 1.05f;
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
    [SerializeField] private Color statTextColor = Color.black;
    [SerializeField] private Color barBackgroundColor = new Color(0.12f, 0.15f, 0.2f, 0.2f);
    [SerializeField] private Color barFillColor = new Color(0.08f, 0.55f, 0.95f, 1f);
    [Range(0.5f, 1f)] [SerializeField] private float barWidthRatio = 0.78f;

    private const float BaseSpeedWeight = 0.8f;
    private const float BoostSpeedWeight = 0.2f;
    private const float SpeedCurvePower = 1.7f;

    private KuuppaKartControls controls;
    private int selectedIndex;
    private GameObject previewInstance;
    private bool ownsPreviewRenderTexture;
    private bool isLoadingScene;
    private Vector2 lastStatsRootSize;
    private bool hasStatsRootSize;

    private void Start()
    {
        if (database == null || database.Count == 0)
        {
            Debug.LogError("CarSelectionMenu: Assign a CarDatabase with at least one car.", this);
            enabled = false;
            return;
        }

        controls = KuuppaKartControls.EnsureInstance();
        Canvas.ForceUpdateCanvases();
        SetupStatsLayout();
        SetupPreviewOutput();

        selectedIndex = Mathf.Max(0, database.FindIndexById(PlayerPrefs.GetString(CarSelectionKeys.SelectedCarIdKey, string.Empty)));
        RefreshSelection();
    }

    private void Update()
    {
        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();

        if (controls != null)
        {
            if (controls.WasPressed(KuuppaKartAction.MenuLeft) || controls.WasPressed(KuuppaKartAction.MenuUp))
                PreviousCar();
            else if (controls.WasPressed(KuuppaKartAction.MenuRight) || controls.WasPressed(KuuppaKartAction.MenuDown))
                NextCar();
            else if (controls.WasPressed(KuuppaKartAction.MenuConfirm))
                ConfirmSelection();
            else if (controls.WasPressed(KuuppaKartAction.MenuBack))
                BackToDifficulty();
        }

        if (previewInstance != null && previewRotationSpeed != 0f)
            previewInstance.transform.Rotate(Vector3.up, previewRotationSpeed * Time.deltaTime, Space.World);
    }

    private void LateUpdate()
    {
        if (statsBarsRoot == null || database == null)
            return;

        Vector2 currentSize = statsBarsRoot.rect.size;

        if (!hasStatsRootSize || (currentSize - lastStatsRootSize).sqrMagnitude > 1f)
        {
            lastStatsRootSize = currentSize;
            hasStatsRootSize = true;

            CarDefinition car = database.GetCar(selectedIndex);

            if (car != null)
                BuildStatsBars(car);
        }
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

    public void PreviousCar() => ChangeCar(-1);
    public void NextCar() => ChangeCar(1);

    public void ConfirmSelection()
    {
        if (isLoadingScene)
            return;

        CarDefinition car = database != null ? database.GetCar(selectedIndex) : null;
        if (car == null)
        {
            Debug.LogError("CarSelectionMenu: The selected car entry is empty.", this);
            return;
        }

        string scene = PlayerPrefs.GetString(CarSelectionKeys.PendingGameplaySceneKey, defaultGameplaySceneName);
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("CarSelectionMenu: Gameplay scene is not included in Build Settings: " + scene, this);
            return;
        }

        PlayerPrefs.SetString(CarSelectionKeys.SelectedCarIdKey, car.StableId);
        PlayerPrefs.Save();
        isLoadingScene = true;
        SceneTransitionManager.LoadScene(scene, LoadSceneMode.Single);
    }

    public void BackToDifficulty()
    {
        if (!Application.CanStreamedLevelBeLoaded(difficultySceneName))
        {
            Debug.LogError("CarSelectionMenu: Difficulty scene is not included in Build Settings: " + difficultySceneName, this);
            return;
        }

        SceneTransitionManager.LoadScene(difficultySceneName);
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
            if (carNameText != null)
                carNameText.text = "MISSING CAR";

            if (descriptionText != null)
                descriptionText.text = "This entry is empty. Check the Car Database.";

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
        return "SPEED: " + SpeedValue(car.topSpeed) + " / " + SpeedValue(car.boostMaxSpeed) +
               "\nACCELERATION: " + car.acceleration.ToString("0.0") + " / " + car.boostAcceleration.ToString("0.0") +
               "\nDRIFTING: " + car.driftBodyAngle.ToString("0") + " deg" +
               "\nHANDLING: " + car.turnSpeed.ToString("0") + " deg/s" +
               "\nWEIGHT: " + car.rigidbodyMass.ToString("0") + " kg";
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
        layout.spacing = 5f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        ContentSizeFitter fitter = statsBarsRoot.GetComponent<ContentSizeFitter>();

        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
    }

    private void BuildStatsBars(CarDefinition car)
    {
        if (statsBarsRoot == null || car == null)
            return;

        ClearStatsBars();

        VerticalLayoutGroup rootLayout = statsBarsRoot.GetComponent<VerticalLayoutGroup>();

        if (rootLayout != null)
            rootLayout.spacing = Mathf.Clamp(statsBarsRoot.rect.height * 0.015f, 2f, 8f);

        AddCategoryBar("SPEED", GetCategoryBarAmount(car, 0));
        AddCategoryBar("ACCELERATION", GetCategoryBarAmount(car, 1));
        AddCategoryBar("DRIFTING", GetCategoryBarAmount(car, 2));
        AddCategoryBar("HANDLING", GetCategoryBarAmount(car, 3));
        AddCategoryBar("WEIGHT", GetCategoryBarAmount(car, 4));

        lastStatsRootSize = statsBarsRoot.rect.size;
        hasStatsRootSize = true;
    }

    private float GetCategoryScore(CarDefinition car, int category)
    {
        switch (category)
        {
            case 0:
                float baseRatio = Ratio(car.topSpeed, c => c.topSpeed);
                float boostRatio = Ratio(car.boostMaxSpeed, c => c.boostMaxSpeed);

                float baseScore = Mathf.Pow(baseRatio, SpeedCurvePower);
                float boostScore = Mathf.Pow(boostRatio, SpeedCurvePower);

                return baseScore * BaseSpeedWeight + boostScore * BoostSpeedWeight;

            case 1:
                return Average(
                    Ratio(car.acceleration, c => c.acceleration),
                    Ratio(car.boostAcceleration, c => c.boostAcceleration)
                );

            case 2:
                return Average(
                    Ratio(car.driftBodyAngle, c => c.driftBodyAngle),
                    Ratio(car.driftLateralSpeed, c => c.driftLateralSpeed),
                    Ratio(car.driftLateralAcceleration, c => c.driftLateralAcceleration),
                    Ratio(car.driftYawMultiplier, c => c.driftYawMultiplier),
                    Ratio(car.driftRecoveryGrip, c => c.driftRecoveryGrip)
                );

            case 3:
                return Average(
                    Ratio(car.turnSpeed, c => c.turnSpeed),
                    Ratio(car.brakePower, c => c.brakePower)
                );

            case 4:
                return car.rigidbodyMass;

            default:
                return 0f;
        }
    }

    private float GetCategoryBarAmount(CarDefinition car, int category)
    {
        float minimum = float.MaxValue;
        float maximum = float.MinValue;

        for (int i = 0; i < database.Count; i++)
        {
            CarDefinition entry = database.GetCar(i);

            if (entry == null)
                continue;

            float score = GetCategoryScore(entry, category);
            minimum = Mathf.Min(minimum, score);
            maximum = Mathf.Max(maximum, score);
        }

        if (minimum == float.MaxValue || maximum - minimum < 0.0001f)
            return 0.65f;

        float normalized = Mathf.InverseLerp(minimum, maximum, GetCategoryScore(car, category));
        return Mathf.Lerp(0.3f, 1f, normalized);
    }

    private float Average(params float[] values)
    {
        float total = 0f;

        for (int i = 0; i < values.Length; i++)
            total += values[i];

        return values.Length > 0 ? total / values.Length : 0f;
    }

    private float Ratio(float value, Func<CarDefinition, float> selector)
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

    private void AddCategoryBar(string title, float amount)
    {
        float rowHeight = Mathf.Max(20f, statsBarsRoot.rect.height / 5f);
        float fontSize = Mathf.Clamp(rowHeight * 0.36f, 11f, 38f);
        float barHeight = Mathf.Clamp(rowHeight * 0.2f, 7f, 18f);
        float barWidth = statsBarsRoot.rect.width * Mathf.Clamp(barWidthRatio, 0.5f, 1f);

        GameObject rowObject = new GameObject(title, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        rowObject.transform.SetParent(statsBarsRoot, false);

        LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
        rowElement.minHeight = rowHeight * 0.8f;
        rowElement.flexibleHeight = 1f;
        rowElement.flexibleWidth = 1f;

        VerticalLayoutGroup row = rowObject.GetComponent<VerticalLayoutGroup>();
        row.childAlignment = TextAnchor.UpperLeft;
        row.spacing = Mathf.Clamp(rowHeight * 0.025f, 1f, 4f);
        row.padding = new RectOffset(0, 0, 0, 0);
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        TMP_Text label = CreateText(rowObject.transform, fontSize, FontStyles.Bold, TextAlignmentOptions.Left);
        label.text = title;
        label.color = Color.black;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(8f, fontSize * 0.65f);
        label.fontSizeMax = fontSize;

        LayoutElement labelElement = label.gameObject.AddComponent<LayoutElement>();
        labelElement.minWidth = labelElement.preferredWidth = barWidth;
        labelElement.minHeight = labelElement.preferredHeight = fontSize * 1.2f;

        GameObject barObject = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        barObject.transform.SetParent(rowObject.transform, false);

        LayoutElement barElement = barObject.GetComponent<LayoutElement>();
        barElement.minWidth = Mathf.Min(60f, barWidth);
        barElement.preferredWidth = barWidth;
        barElement.flexibleWidth = 0f;
        barElement.minHeight = barElement.preferredHeight = barHeight;

        Image background = barObject.GetComponent<Image>();
        background.color = barBackgroundColor;
        background.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(barObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fill = fillObject.GetComponent<Image>();
        fill.type = Image.Type.Simple;
        fill.color = barFillColor;
        fill.raycastTarget = false;
    }

    private TMP_Text CreateText(Transform parent, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();

        if (carNameText != null && carNameText.font != null)
            text.font = carNameText.font;

        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = statTextColor;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    private string SpeedValue(float value) => (value * speedometerDisplayMultiplier).ToString("0") + " " + speedUnit;

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
            return;

        if (previewCamera == null)
        {
            GameObject go = new GameObject("CarPreviewCamera");
            previewCamera = go.AddComponent<Camera>();
            previewCamera.fieldOfView = 35f;
            previewCamera.transform.position = previewAnchor.position + generatedCameraOffset;
            previewCamera.transform.LookAt(previewAnchor.position + previewLookAtOffset);
        }

        if (previewImage == null)
            return;

        if (previewRenderTexture == null)
        {
            int width = Mathf.Clamp(previewTextureWidth, 256, 2048);
            Rect rect = previewImage.rectTransform.rect;
            float aspect = rect.width > 1f && rect.height > 1f ? rect.width / rect.height : 16f / 9f;
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

        if (mainCamera != null && mainCamera != previewCamera)
            mainCamera.cullingMask &= ~(1 << Mathf.Clamp(previewLayer, 0, 31));
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
        previewInstance.transform.localScale = car.visualLocalScale * database.GetVisualScaleMultiplier(car);
        SetLayerRecursively(previewInstance.transform, Mathf.Clamp(previewLayer, 0, 31));
        MakeVisualOnly(previewInstance);

        if (autoFramePreview && previewCamera != null)
            FramePreview(previewInstance);
    }

    private void FramePreview(GameObject carObject)
    {
        if (previewCamera == null || carObject == null)
            return;

        Renderer[] renderers = carObject.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 target = bounds.center + previewLookAtOffset;
        Vector3 localDirection = previewCameraLocalDirection.sqrMagnitude > 0.001f ? previewCameraLocalDirection : new Vector3(-0.8f, 1f, -1.8f);
        Vector3 direction = carObject.transform.TransformDirection(localDirection).normalized;

        previewCamera.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);

        Vector3 e = bounds.extents;
        Vector3 right = previewCamera.transform.right;
        Vector3 up = previewCamera.transform.up;
        Vector3 forward = previewCamera.transform.forward;

        float halfWidth = Mathf.Abs(right.x) * e.x + Mathf.Abs(right.y) * e.y + Mathf.Abs(right.z) * e.z;
        float halfHeight = Mathf.Abs(up.x) * e.x + Mathf.Abs(up.y) * e.y + Mathf.Abs(up.z) * e.z;
        float halfDepth = Mathf.Abs(forward.x) * e.x + Mathf.Abs(forward.y) * e.y + Mathf.Abs(forward.z) * e.z;

        float vertical = previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float horizontal = Mathf.Atan(Mathf.Tan(vertical) * Mathf.Max(0.1f, previewCamera.aspect));

        float distance = Mathf.Max(halfWidth / Mathf.Tan(horizontal), halfHeight / Mathf.Tan(vertical));
        distance = (distance + halfDepth) * Mathf.Max(1f, previewFramingPadding);

        previewCamera.transform.position = target + direction * Mathf.Max(1.5f, distance);
        previewCamera.transform.LookAt(target, Vector3.up);
        previewCamera.nearClipPlane = 0.05f;
        previewCamera.farClipPlane = Mathf.Max(500f, distance * 5f);
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