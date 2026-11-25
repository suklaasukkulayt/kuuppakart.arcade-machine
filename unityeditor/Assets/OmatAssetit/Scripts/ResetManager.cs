using UnityEngine;
using UnityEngine.SceneManagement;

public class ResetManager : MonoBehaviour
{
    public void RestartCurrentScene()
    {
        if (!Application.CanStreamedLevelBeLoaded("StartScreen"))
        {
            Debug.LogError("ResetManager has a small message for u: YOU ARE A BOT BRO how do you not HAVE STARTSCREEN in BUILD SETTINGs YOU ARE A POTATO !!!1");
            return;
        }
        Time.timeScale = 1f;
        SceneTransitionManager.LoadScene("StartScreen");
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
