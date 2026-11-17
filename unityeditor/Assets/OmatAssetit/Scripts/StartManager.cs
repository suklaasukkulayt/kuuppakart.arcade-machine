using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuManager : MonoBehaviour
{
    private KuuppaKartControls controls;

    private void Awake()
    {
        controls = GetComponent<KuuppaKartControls>();
        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name != "StartScreen")
            return;

        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();
        if (controls.StartPressed)
            StartGame();
    }

    public void StartGame()
    {
        if (!Application.CanStreamedLevelBeLoaded("DifficultySelect"))
        {
            Debug.LogError("StartMenuManager: DifficultySelect is not included in Build Settings.");
            return;
        }
        SceneManager.LoadScene("DifficultySelect");
    }
}