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

    [Header("Marker")]
    [SerializeField, Min(4f)] private float markerSize = 14f;
    [SerializeField] private Color playerColor = new Color(0.2f, 1f, 0.45f, 1f);
    [SerializeField] private Color aiColor = new Color(0.15f, 0.55f, 1f, 1f);

    [Header("Map Colors")]
    [SerializeField] private Color roadColor = new Color(0.43f, 0.47f, 0.53f, 1f);
    [SerializeField] private Color objectColor = new Color(0.72f, 0.77f, 0.84f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0.045f, 0.055f, 0.075f, 1f);

    private readonly List<GameObject> proxyObjects = new List<GameObject>();
    private readonly Dictionary<CarIdentify, RectTransform> carMarkers = new Dictionary<CarIdentify, RectTransform>();
    private Transform trackPiecesRoot;
    private Camera mapCamera;
    private RenderTexture mapTexture;
    private Material roadMaterial;
    private Material objectMaterial;
    private Texture2D markerTexture;
    private Sprite markerSprite;
    private Bounds mapBounds;
    private float halfWidth;
    private float halfHeight;
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

        int minimapLayer = LayerMask.NameToLayer(minimapLayerName);
        if (minimapLayer < 0)
        {
            Debug.LogError("Create layer " + minimapLayerName + ".", this);
            return;
        }

        GameObject trackObject = GameObject.Find("Track");
        trackPiecesRoot = trackObject != null ? trackObject.transform.Find("TrackPieces") : null;
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
            Debug.LogError("No roads found under TrackPieces.", this);
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

        int width = Mathf.Max(128, Mathf.RoundToInt(textureHeight * aspect));
        mapTexture = new RenderTexture(width, textureHeight, 24, RenderTextureFormat.ARGB32);
        mapTexture.name = "Track Minimap Texture";
        mapTexture.filterMode = FilterMode.Bilinear;
        mapTexture.antiAliasing = 2;
        mapTexture.Create();
        mapCamera.targetTexture = mapTexture;

        mapImage.texture = mapTexture;
        mapImage.color = Color.white;
        if (GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();
        CreateMarkerSprite();
        CreateCarMarkers();
        initialized = true;
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

    private void CreateMarkerSprite()
    {
        const int size = 64;
        markerTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        markerTexture.name = "Minimap Marker Texture";
        markerTexture.filterMode = FilterMode.Bilinear;
        markerTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.SmoothStep(0.88f, 1f, distance);
                markerTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        markerTexture.Apply();
        markerSprite = Sprite.Create(markerTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void CreateCarMarkers()
    {
        CarIdentify[] cars = FindObjectsByType<CarIdentify>(FindObjectsSortMode.None);
        if (cars.Length == 0)
            Debug.LogWarning("Minimap didn't find player or ai.", this);

        foreach (CarIdentify car in cars)
        {
            if (car == null)
                continue;

            bool isAI = car.kind == CarKind.AI || car.GetComponent<AICar>() != null || car.GetComponentInChildren<AICar>() != null || car.GetComponentInParent<AICar>() != null;
            Color markerColor = isAI ? aiColor : playerColor;
            GameObject markerObject = new GameObject(isAI ? "AI Minimap Dot" : "Player Minimap Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            markerObject.transform.SetParent(mapImage.transform, false);

            RectTransform markerRect = markerObject.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = Vector2.one * markerSize;

            Image markerImage = markerObject.GetComponent<Image>();
            markerImage.sprite = markerSprite;
            markerImage.color = markerColor;
            markerImage.raycastTarget = false;

            carMarkers.Add(car, markerRect);
            markerRect.SetAsLastSibling();
        }
    }

    private void LateUpdate()
    {
        if (!initialized || mapImage == null)
            return;

        Rect rect = mapImage.rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        foreach (KeyValuePair<CarIdentify, RectTransform> entry in carMarkers)
        {
            if (entry.Key == null || entry.Value == null)
                continue;

            Vector3 worldPosition = entry.Key.transform.position;
            float normalizedX = (worldPosition.x - mapBounds.center.x) / halfWidth;
            float normalizedY = (worldPosition.z - mapBounds.center.z) / halfHeight;
            entry.Value.anchoredPosition = new Vector2(normalizedX * rect.width * 0.5f, normalizedY * rect.height * 0.5f);
        }
    }

    private void OnDestroy()
    {
        foreach (GameObject proxy in proxyObjects)
        {
            if (proxy != null)
                Destroy(proxy);
        }

        foreach (RectTransform marker in carMarkers.Values)
        {
            if (marker != null)
                Destroy(marker.gameObject);
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
        if (markerTexture != null)
            Destroy(markerTexture);
    }
}
