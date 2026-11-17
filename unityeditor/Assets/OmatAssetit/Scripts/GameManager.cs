using UnityEngine;
using UnityEngine.SceneManagement;

public enum RacePhase
{
    Countdown,
    Racing,
    Finished
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public RacePhase Phase { get; set; } = RacePhase.Countdown;

    private static readonly string[] GameplayScenes =
    {
        "Game",
        "KKartOG",
        "KKartOG-Halloween",
        "Level2",
        "WeirdMountains"
    };

    public static bool IsGameplayScene(string sceneName)
    {
        for (int i = 0; i < GameplayScenes.Length; i++)
            if (string.Equals(GameplayScenes[i], sceneName, System.StringComparison.Ordinal))
                return true;
        return false;
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
        KuuppaKartControls.EnsureInstance();
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsGameplayScene(scene.name))
            Phase = RacePhase.Countdown;
    }
}
