using UnityEngine;
public enum Difficulty { Easy, Normal, Hard }
public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }
    public Difficulty SelectedDifficulty { get; set; } = Difficulty.Normal;
    public float SelectedFov { get; set; } = 80f;
    public bool FirstPerson { get; set; } = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}