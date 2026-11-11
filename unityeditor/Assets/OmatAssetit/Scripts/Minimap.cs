using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class Minimap : MonoBehaviour
{
    [Header("Map")]
    [SerializeField] private RawImage mapImage;
    [SerializeField] private string minimapLayerName = "MinimapOnly";
    [SerializeField, Min(1f)] private float mapPadding = 1.1f;
    [SerializeField, Min(128)] private int textureHeight = 512;
    [SerializeField, Min(0f)] private float cornerRadius = 22f;
    [SerializeField, Min(0f)] private float frameThickness = 3f;

    [Header("Marker")]
    [SerializeField, Min(4f)] private float markerSize = 17f;
    [SerializeField] private Color playerColor = new Color(0.5f, 1f, 0.12f, 1f);
    [SerializeField] private Color aiColor = new Color(0.02f, 0.78f, 1f, 1f);

    [Header("AI Visibility")]
    [SerializeField, Tooltip("Raahaa tähän AICar-komponentit niistä AI-autoista, joita et halua näyttää minimapissa.")]
    private List<AICar> hiddenAICars = new List<AICar>();

    [Header("Map Colors")]
    [SerializeField] private Color roadColor = new Color(0.35f, 0.4f, 0.48f, 1f);
    [SerializeField] private Color objectColor = new Color(0.58f, 0.65f, 0.74f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0.035f, 0.05f, 0.075f, 1f);
    [SerializeField] private Color frameColor = new Color(0.34f, 0.49f, 0.64f, 1f);

    private readonly List<GameObject> proxyObjects = new List<GameObject>();
    private readonly Dictionary<Transform, RectTransform> carMarkers = new Dictionary<Transform, RectTransform>();
    private readonly Dictionary<Camera, int> originalCameraMasks = new Dictionary<Camera, int>();
    private Camera mapCamera;
    private RenderTexture mapTexture;
    private Material roadMaterial;
    private Material objectMaterial;
    private Texture2D markerTexture;
    private Texture2D glowTexture;
    private Sprite markerSprite;
    private Sprite glowSprite;
    private GameObject roundedFrameObject;
    private Transform originalMapParent;
    private int originalMapSiblingIndex;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;
    private Vector2 originalPivot;
    private Vector2 originalAnchoredPosition;
    private Vector2 originalSizeDelta;
    private Vector3 originalLocalScale;
    private Quaternion originalLocalRotation;
    private Bounds mapBounds;
    private float halfWidth;
    private float halfHeight;
    private int minimapLayer;
    private bool initialized;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (mapImage == null)
            mapImage = GetComponent<RawImage>();

        if (mapImage == null)
        {
            Debug.LogError("RawImage component required for Minimap", this);
            return;
        }

        minimapLayer = LayerMask.NameToLayer(minimapLayerName);
        if (minimapLayer < 0)
        {
            Debug.LogError("Create layer " + minimapLayerName + ".", this);
            return;
        }

        GameObject trackObject = GameObject.Find("Track");
        Transform trackPiecesRoot = trackObject != null ? trackObject.transform.Find("TrackPieces") : null;
        if (trackPiecesRoot == null)
        {
            Debug.LogError("Track/TrackPieces object not found in this scene.", this);
            return;
        }

        MeshRenderer[] sourceRenderers = trackPiecesRoot.GetComponentsInChildren<MeshRenderer>(true);
        List<MeshRenderer> validRenderers = new List<MeshRenderer>();
        bool hasBounds = false;

        foreach (MeshRenderer source in sourceRenderers)
        {
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy)
                continue;

            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
                continue;

            validRenderers.Add(source);
            if (!hasBounds)
            {
                mapBounds = source.bounds;
                hasBounds = true;
            }
            else
            {
                mapBounds.Encapsulate(source.bounds);
            }
        }

        if (validRenderers.Count == 0 || !hasBounds)
        {
            Debug.LogError("No MeshRenderer/MeshFilter objects found under TrackPieces.", this);
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            Debug.LogError("Minimap Unlit shader not found.", this);
            return;
        }

        roadMaterial = CreateFlatMaterial(shader, roadColor);
        objectMaterial = CreateFlatMaterial(shader, objectColor);

        foreach (MeshRenderer source in validRenderers)
        {
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            GameObject proxy = new GameObject(source.gameObject.name + "_MinimapProxy");
            proxy.layer = minimapLayer;
            proxy.transform.SetParent(source.transform, false);
            proxy.transform.localPosition = Vector3.zero;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one;
            proxy.hideFlags = HideFlags.HideInHierarchy;

            MeshFilter proxyFilter = proxy.AddComponent<MeshFilter>();
            proxyFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer proxyRenderer = proxy.AddComponent<MeshRenderer>();
            string objectName = source.gameObject.name.ToLowerInvariant();
            bool isRoad = objectName.Contains("road") || objectName.Contains("bridge");
            Material flatMaterial = isRoad ? roadMaterial : objectMaterial;
            int subMeshCount = Mathf.Max(1, sourceFilter.sharedMesh.subMeshCount);
            Material[] proxyMaterials = new Material[subMeshCount];

            for (int i = 0; i < proxyMaterials.Length; i++)
                proxyMaterials[i] = flatMaterial;

            proxyRenderer.sharedMaterials = proxyMaterials;
            proxyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            proxyRenderer.receiveShadows = false;
            proxyRenderer.lightProbeUsage = LightProbeUsage.Off;
            proxyRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            proxyRenderer.allowOcclusionWhenDynamic = false;
            proxyObjects.Add(proxy);
        }

        SetupRoundedFrame();

        Rect imageRect = mapImage.rectTransform.rect;
        float aspect = imageRect.width > 0f && imageRect.height > 0f ? imageRect.width / imageRect.height : 1f;
        halfHeight = Mathf.Max(mapBounds.extents.z, mapBounds.extents.x / aspect) * mapPadding;
        halfHeight = Mathf.Max(halfHeight, 1f);
        halfWidth = halfHeight * aspect;

        GameObject cameraObject = new GameObject("Track Minimap Camera");
        cameraObject.hideFlags = HideFlags.HideInHierarchy;
        mapCamera = cameraObject.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = halfHeight;
        mapCamera.aspect = aspect;
        mapCamera.transform.position = new Vector3(mapBounds.center.x, mapBounds.max.y + Mathf.Max(100f, mapBounds.extents.y + 50f), mapBounds.center.z);
        mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        mapCamera.nearClipPlane = 0.1f;
        mapCamera.farClipPlane = Mathf.Max(1000f, mapCamera.transform.position.y - mapBounds.min.y + 100f);
        mapCamera.cullingMask = 1 << minimapLayer;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = backgroundColor;
        mapCamera.useOcclusionCulling = false;
        mapCamera.allowHDR = false;

        ExcludeMinimapLayerFromOtherCameras();

        int width = Mathf.Max(128, Mathf.RoundToInt(textureHeight * aspect));
        mapTexture = new RenderTexture(width, textureHeight, 24, RenderTextureFormat.ARGB32);
        mapTexture.name = "Track Minimap Texture";
        mapTexture.filterMode = FilterMode.Bilinear;
        mapTexture.antiAliasing = 2;
        mapTexture.Create();
        mapCamera.targetTexture = mapTexture;

        mapImage.texture = mapTexture;
        mapImage.color = Color.white;
        CreateMarkerSprites();
        RefreshCarMarkers();
        initialized = true;
    }

    private void ExcludeMinimapLayerFromOtherCameras()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int layerMask = 1 << minimapLayer;

        foreach (Camera camera in cameras)
        {
            if (camera == null || camera == mapCamera || originalCameraMasks.ContainsKey(camera))
                continue;

            originalCameraMasks.Add(camera, camera.cullingMask);
            camera.cullingMask &= ~layerMask;
        }
    }

    private Material CreateFlatMaterial(Shader shader, Color color)
    {
        Material material = new Material(shader);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        material.enableInstancing = true;
        return material;
    }

    private void SetupRoundedFrame()
    {
        RectTransform mapRect = mapImage.rectTransform;
        originalMapParent = mapRect.parent;

        if (originalMapParent == null)
            return;

        originalMapSiblingIndex = mapRect.GetSiblingIndex();
        originalAnchorMin = mapRect.anchorMin;
        originalAnchorMax = mapRect.anchorMax;
        originalPivot = mapRect.pivot;
        originalAnchoredPosition = mapRect.anchoredPosition;
        originalSizeDelta = mapRect.sizeDelta;
        originalLocalScale = mapRect.localScale;
        originalLocalRotation = mapRect.localRotation;

        RectTransform frameRect = new GameObject(
            "Minimap Rounded Frame",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(MinimapRoundedGraphic),
            typeof(Mask)).GetComponent<RectTransform>();
        roundedFrameObject = frameRect.gameObject;
        frameRect.SetParent(originalMapParent, false);
        frameRect.anchorMin = originalAnchorMin;
        frameRect.anchorMax = originalAnchorMax;
        frameRect.pivot = originalPivot;
        frameRect.anchoredPosition = originalAnchoredPosition;
        frameRect.sizeDelta = originalSizeDelta;
        frameRect.localScale = originalLocalScale;
        frameRect.localRotation = originalLocalRotation;
        frameRect.SetSiblingIndex(originalMapSiblingIndex);

        MinimapRoundedGraphic frameGraphic = frameRect.GetComponent<MinimapRoundedGraphic>();
        frameGraphic.color = frameColor;
        frameGraphic.raycastTarget = false;
        frameGraphic.SetRadius(cornerRadius);

        Mask frameMask = frameRect.GetComponent<Mask>();
        frameMask.showMaskGraphic = true;

        RectTransform clipRect = new GameObject(
            "Minimap Rounded Clip",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(MinimapRoundedGraphic),
            typeof(Mask)).GetComponent<RectTransform>();
        clipRect.SetParent(frameRect, false);
        clipRect.anchorMin = Vector2.zero;
        clipRect.anchorMax = Vector2.one;
        clipRect.offsetMin = Vector2.one * frameThickness;
        clipRect.offsetMax = Vector2.one * -frameThickness;
        clipRect.localScale = Vector3.one;

        MinimapRoundedGraphic clipGraphic = clipRect.GetComponent<MinimapRoundedGraphic>();
        clipGraphic.color = Color.white;
        clipGraphic.raycastTarget = false;
        clipGraphic.SetRadius(Mathf.Max(0f, cornerRadius - frameThickness));

        Mask clipMask = clipRect.GetComponent<Mask>();
        clipMask.showMaskGraphic = false;

        mapRect.SetParent(clipRect, false);
        mapRect.anchorMin = Vector2.zero;
        mapRect.anchorMax = Vector2.one;
        mapRect.offsetMin = Vector2.zero;
        mapRect.offsetMax = Vector2.zero;
        mapRect.pivot = new Vector2(0.5f, 0.5f);
        mapRect.anchoredPosition = Vector2.zero;
        mapRect.localScale = Vector3.one;
        mapRect.localRotation = Quaternion.identity;

        RectMask2D oldRectMask = mapImage.GetComponent<RectMask2D>();

        if (oldRectMask != null)
            Destroy(oldRectMask);
    }

    private void CreateMarkerSprites()
    {
        const int circleSize = 64;
        markerTexture = new Texture2D(circleSize, circleSize, TextureFormat.RGBA32, false);
        markerTexture.name = "Minimap Marker Texture";
        markerTexture.filterMode = FilterMode.Bilinear;
        markerTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < circleSize; y++)
        {
            for (int x = 0; x < circleSize; x++)
            {
                float dx = (x + 0.5f - circleSize * 0.5f) / (circleSize * 0.5f);
                float dy = (y + 0.5f - circleSize * 0.5f) / (circleSize * 0.5f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.SmoothStep(0.84f, 1f, distance);
                markerTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        markerTexture.Apply();
        markerSprite = Sprite.Create(markerTexture, new Rect(0f, 0f, circleSize, circleSize), new Vector2(0.5f, 0.5f), circleSize);

        const int glowSize = 128;
        glowTexture = new Texture2D(glowSize, glowSize, TextureFormat.RGBA32, false);
        glowTexture.name = "Minimap Marker Glow";
        glowTexture.filterMode = FilterMode.Bilinear;
        glowTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < glowSize; y++)
        {
            for (int x = 0; x < glowSize; x++)
            {
                float dx = (x + 0.5f - glowSize * 0.5f) / (glowSize * 0.5f);
                float dy = (y + 0.5f - glowSize * 0.5f) / (glowSize * 0.5f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.3f) * 0.85f;
                glowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        glowTexture.Apply();
        glowSprite = Sprite.Create(glowTexture, new Rect(0f, 0f, glowSize, glowSize), new Vector2(0.5f, 0.5f), glowSize);
    }

    private bool IsAIHidden(AICar ai)
    {
        return ai != null && hiddenAICars != null && hiddenAICars.Contains(ai);
    }

    private void RefreshCarMarkers()
    {
        if (hiddenAICars != null)
        {
            foreach (AICar hiddenAI in hiddenAICars)
            {
                if (hiddenAI == null)
                    continue;

                if (carMarkers.TryGetValue(hiddenAI.transform, out RectTransform hiddenMarker))
                {
                    if (hiddenMarker != null)
                        Destroy(hiddenMarker.gameObject);

                    carMarkers.Remove(hiddenAI.transform);
                }
            }
        }

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        foreach (Player player in players)
        {
            if (player != null)
                AddCarMarker(player.transform, playerColor, "Player Minimap Dot");
        }

        AICar[] aiCars = FindObjectsByType<AICar>(FindObjectsSortMode.None);

        foreach (AICar ai in aiCars)
        {
            if (ai != null && !IsAIHidden(ai))
                AddCarMarker(ai.transform, aiColor, "AI Minimap Dot");
        }

        CarIdentify[] identifiedCars = FindObjectsByType<CarIdentify>(FindObjectsSortMode.None);

        foreach (CarIdentify car in identifiedCars)
        {
            if (car == null)
                continue;

            AICar ai = car.GetComponentInParent<AICar>();

            if (ai == null)
                ai = car.GetComponentInChildren<AICar>();

            Player player = car.GetComponentInParent<Player>();

            if (player == null)
                player = car.GetComponentInChildren<Player>();

            if (ai != null)
            {
                if (!IsAIHidden(ai))
                    AddCarMarker(ai.transform, aiColor, "AI Minimap Dot");
            }
            else if (player != null)
            {
                AddCarMarker(player.transform, playerColor, "Player Minimap Dot");
            }
            else if (car.kind == CarKind.AI)
            {
                bool isHidden = false;

                if (hiddenAICars != null)
                {
                    foreach (AICar hiddenAI in hiddenAICars)
                    {
                        if (hiddenAI != null && (hiddenAI.transform == car.transform || car.transform.IsChildOf(hiddenAI.transform)))
                        {
                            isHidden = true;
                            break;
                        }
                    }
                }

                if (!isHidden)
                    AddCarMarker(car.transform, aiColor, "AI Minimap Dot");
            }
            else
            {
                AddCarMarker(car.transform, playerColor, "Player Minimap Dot");
            }
        }
    }

    private void AddCarMarker(Transform target, Color color, string markerName)
    {
        if (target == null || carMarkers.ContainsKey(target))
            return;

        GameObject markerObject = new GameObject(markerName, typeof(RectTransform));
        markerObject.transform.SetParent(mapImage.transform, false);

        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = Vector2.one * markerSize * 2.2f;

        GameObject glowObject = new GameObject("Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glowObject.transform.SetParent(markerRect, false);

        RectTransform glowRect = glowObject.GetComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = Vector2.zero;
        glowRect.offsetMax = Vector2.zero;

        Image glowImage = glowObject.GetComponent<Image>();
        glowImage.sprite = glowSprite;
        glowImage.color = new Color(color.r, color.g, color.b, 0.9f);
        glowImage.raycastTarget = false;

        GameObject ringObject = new GameObject("White Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ringObject.transform.SetParent(markerRect, false);

        RectTransform ringRect = ringObject.GetComponent<RectTransform>();
        ringRect.anchorMin = new Vector2(0.5f, 0.5f);
        ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.sizeDelta = Vector2.one * markerSize * 1.02f;

        Image ringImage = ringObject.GetComponent<Image>();
        ringImage.sprite = markerSprite;
        ringImage.color = Color.white;
        ringImage.raycastTarget = false;

        GameObject coreObject = new GameObject("Marker Core", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        coreObject.transform.SetParent(markerRect, false);

        RectTransform coreRect = coreObject.GetComponent<RectTransform>();
        coreRect.anchorMin = new Vector2(0.5f, 0.5f);
        coreRect.anchorMax = new Vector2(0.5f, 0.5f);
        coreRect.pivot = new Vector2(0.5f, 0.5f);
        coreRect.sizeDelta = Vector2.one * markerSize * 0.72f;

        Image coreImage = coreObject.GetComponent<Image>();
        coreImage.sprite = markerSprite;
        coreImage.color = color;
        coreImage.raycastTarget = false;

        carMarkers.Add(target, markerRect);
        markerRect.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        if (!initialized || mapImage == null)
            return;

        if (Time.frameCount % 60 == 0)
            ExcludeMinimapLayerFromOtherCameras();

        if (Time.frameCount % 30 == 0)
            RefreshCarMarkers();

        Rect rect = mapImage.rectTransform.rect;

        if (rect.width <= 0f || rect.height <= 0f)
            return;

        List<Transform> missingTargets = null;

        foreach (KeyValuePair<Transform, RectTransform> entry in carMarkers)
        {
            if (entry.Key == null || entry.Value == null)
            {
                if (missingTargets == null)
                    missingTargets = new List<Transform>();

                missingTargets.Add(entry.Key);
                continue;
            }

            Vector3 worldPosition = entry.Key.position;
            float normalizedX = (worldPosition.x - mapBounds.center.x) / halfWidth;
            float normalizedY = (worldPosition.z - mapBounds.center.z) / halfHeight;

            entry.Value.anchoredPosition = new Vector2(normalizedX * rect.width * 0.5f, normalizedY * rect.height * 0.5f);
        }

        if (missingTargets != null)
        {
            foreach (Transform target in missingTargets)
                carMarkers.Remove(target);
        }
    }

    private void OnDestroy()
    {
        if (mapImage != null && originalMapParent != null)
        {
            RectTransform mapRect = mapImage.rectTransform;
            mapRect.SetParent(originalMapParent, false);
            mapRect.anchorMin = originalAnchorMin;
            mapRect.anchorMax = originalAnchorMax;
            mapRect.pivot = originalPivot;
            mapRect.anchoredPosition = originalAnchoredPosition;
            mapRect.sizeDelta = originalSizeDelta;
            mapRect.localScale = originalLocalScale;
            mapRect.localRotation = originalLocalRotation;
        }

        foreach (RectTransform marker in carMarkers.Values)
        {
            if (marker != null)
                Destroy(marker.gameObject);
        }

        foreach (GameObject proxy in proxyObjects)
        {
            if (proxy != null)
                Destroy(proxy);
        }

        foreach (KeyValuePair<Camera, int> entry in originalCameraMasks)
        {
            if (entry.Key != null)
                entry.Key.cullingMask = entry.Value;
        }

        if (mapImage != null && mapTexture != null && mapImage.texture == mapTexture)
            mapImage.texture = null;

        if (mapCamera != null)
            Destroy(mapCamera.gameObject);

        if (mapTexture != null)
            Destroy(mapTexture);

        if (roadMaterial != null)
            Destroy(roadMaterial);

        if (objectMaterial != null)
            Destroy(objectMaterial);

        if (markerSprite != null)
            Destroy(markerSprite);

        if (glowSprite != null)
            Destroy(glowSprite);

        if (markerTexture != null)
            Destroy(markerTexture);

        if (glowTexture != null)
            Destroy(glowTexture);

        if (roundedFrameObject != null)
            Destroy(roundedFrameObject);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public class MinimapRoundedGraphic : MaskableGraphic
{
    [SerializeField] private float cornerRadius = 22f;

    public void SetRadius(float radius)
    {
        cornerRadius = Mathf.Max(0f, radius);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;

        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float radius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
        List<Vector2> points = new List<Vector2>();

        if (radius <= 0.01f)
        {
            points.Add(new Vector2(rect.xMin, rect.yMin));
            points.Add(new Vector2(rect.xMax, rect.yMin));
            points.Add(new Vector2(rect.xMax, rect.yMax));
            points.Add(new Vector2(rect.xMin, rect.yMax));
        }
        else
        {
            AddArc(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, -90f, 0f);
            AddArc(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f);
            AddArc(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f);
            AddArc(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f);
        }

        vertexHelper.AddVert(new Vector3(rect.center.x, rect.center.y), color, Vector2.zero);

        for (int i = 0; i < points.Count; i++)
            vertexHelper.AddVert(points[i], color, Vector2.zero);

        for (int i = 0; i < points.Count; i++)
        {
            int current = i + 1;
            int next = (i + 1) % points.Count + 1;
            vertexHelper.AddTriangle(0, current, next);
        }
    }

    private void AddArc(List<Vector2> points, Vector2 center, float radius, float startAngle, float endAngle)
    {
        const int segments = 10;

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segments) * Mathf.Deg2Rad;
            points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }
}