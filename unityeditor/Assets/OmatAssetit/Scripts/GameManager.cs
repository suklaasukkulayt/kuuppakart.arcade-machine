using UnityEngine;
using UnityEngine.SceneManagement;

public enum RacePhase { Countdown, Racing, Finished }
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public RacePhase Phase { get; set; } = RacePhase.Countdown;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Game")
            Phase = RacePhase.Countdown;
    }
}
