using UnityEngine;

[DefaultExecutionOrder(-32000)]
public sealed class KuuppaKartLowEndBootstrap : MonoBehaviour
{
    private static KuuppaKartLowEndBootstrap instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallBeforeFirstScene()
    {
        if (instance != null)
            return;

        GameObject bootstrapObject = new GameObject("KuuppaKart Graphics Bootstrap");
        DontDestroyOnLoad(bootstrapObject);
        instance = bootstrapObject.AddComponent<KuuppaKartLowEndBootstrap>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        KuuppaKartGraphicsSettings.ApplySavedSettingsOnStartup();
        Debug.Log("[KuuppaKart] Saved graphics settings applied.");
    }
}
