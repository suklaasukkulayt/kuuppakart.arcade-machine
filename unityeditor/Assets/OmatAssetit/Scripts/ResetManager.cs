using UnityEngine;
using UnityEngine.SceneManagement;

public class ResetManager : MonoBehaviour
{
    public void RestartCurrentScene()
    {
        if (!Application.CanStreamedLevelBeLoaded("StartScreen"))
        {
            Debug.LogError("ResetManager: StartScreen is not included in Build Settings.");
            return;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene("StartScreen");
    }

    private void Update()
    {
        if (!GameManager.IsGameplayScene(SceneManager.GetActiveScene().name))
            return;
        if (GameManager.Instance == null)
            return;

        KuuppaKartPauseMenu pauseMenu = FindFirstObjectByType<KuuppaKartPauseMenu>();
        if (pauseMenu != null && pauseMenu.IsOpen)
            return;

        KuuppaKartControls controls = KuuppaKartControls.EnsureInstance();
        if (controls.ResetPressed)
            RestartCurrentScene();
    }
}
