using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneTransitionManager : MonoBehaviour
{
    private const float ClosedRadius = -0.08f;
    private const float OpenRadius = 1.12f;
    private const float CloseDuration = 0.55f;
    private const float OpenDuration = 0.7f;
    public static SceneTransitionManager Instance { get; private set; }
    public static bool IsTransitioning => Instance != null && Instance.transitioning;
    private Material transitionMaterial;
    private Coroutine activeRoutine;
    private bool transitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        GameObject manager = new GameObject("KuuppaKart Scene Transitions");
        manager.AddComponent<SceneTransitionManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateOverlay();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (transitionMaterial != null)
            Destroy(transitionMaterial);
        Instance = null;
    }

    private void CreateOverlay()
    {
        GameObject canvasObject = new GameObject("Transition Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        GameObject imageObject = new GameObject("Radial Black Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        Shader shader = Resources.Load<Shader>("KuuppaKartRadialReveal");
        if (shader == null)
            shader = Shader.Find("UI/KuuppaKartRadialReveal");

        if (shader == null)
        {
            Debug.LogError("SceneTransitionManager: KuuppaKartRadialReveal shader was not found. Put it in Assets/Resources/KuuppaKartRadialReveal.shader.");
            image.enabled = false;
            return;
        }

        transitionMaterial = new Material(shader);
        transitionMaterial.name = "KuuppaKart Radial Transition Runtime";
        transitionMaterial.SetFloat("_Radius", ClosedRadius);
        transitionMaterial.SetFloat("_Softness", 0.012f);
        image.material = transitionMaterial;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (transitioning || transitionMaterial == null)
            return;

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(RevealScene());
    }

    private IEnumerator RevealScene()
    {
        transitioning = true;
        SetRadius(ClosedRadius);
        yield return AnimateRadius(ClosedRadius, OpenRadius, OpenDuration);
        transitioning = false;
        activeRoutine = null;
    }

    public static void LoadScene(string sceneName)
    {
        LoadScene(sceneName, LoadSceneMode.Single);
    }

    public static void LoadScene(string sceneName, LoadSceneMode mode)
    {
        if (Instance == null)
            Bootstrap();

        if (Instance == null)
        {
            Debug.LogError("SceneTransitionManager could not be created.");
            return;
        }

        Instance.BeginSceneChange(sceneName, mode);
    }

    private void BeginSceneChange(string sceneName, LoadSceneMode mode)
    {
        if (transitioning)
            return;

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("SceneTransitionManager: Scene is not included in Build Settings: " + sceneName);
            return;
        }

        if (transitionMaterial == null)
        {
            Debug.LogError("SceneTransitionManager: Transition material is missing.");
            SceneManager.LoadScene(sceneName, mode);
            return;
        }

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(ChangeScene(sceneName, mode));
    }

    private IEnumerator ChangeScene(string sceneName, LoadSceneMode mode)
    {
        transitioning = true;
        yield return AnimateRadius(OpenRadius, ClosedRadius, CloseDuration);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);
        if (operation == null)
        {
            Debug.LogError("SceneTransitionManager: Could not start loading scene: " + sceneName);
            transitioning = false;
            yield return AnimateRadius(ClosedRadius, OpenRadius, OpenDuration);
            activeRoutine = null;
            yield break;
        }

        while (!operation.isDone)
            yield return null;

        SetRadius(ClosedRadius);
        yield return AnimateRadius(ClosedRadius, OpenRadius, OpenDuration);

        transitioning = false;
        activeRoutine = null;
    }

    private IEnumerator AnimateRadius(float from, float to, float duration)
    {
        if (transitionMaterial == null)
            yield break;

        if (duration <= 0f)
        {
            SetRadius(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            SetRadius(Mathf.Lerp(from, to, eased));
            yield return null;
        }

        SetRadius(to);
    }

    private void SetRadius(float radius)
    {
        if (transitionMaterial != null)
            transitionMaterial.SetFloat("_Radius", radius);
    }
}
