using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-900)]
[RequireComponent(typeof(GraphicRaycaster))]
public sealed class KuuppaKartMouseMenu : MonoBehaviour
{
    private GraphicRaycaster raycaster;
    private KuuppaKartControls controls;

    private void Awake()
    {
        raycaster = GetComponent<GraphicRaycaster>();
        controls = KuuppaKartControls.EnsureInstance();
    }

    private void Update()
    {
        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();
        bool shouldEnable = controls.MouseMenuInputEnabled || controls.TouchInputMode;
        if (raycaster.enabled != shouldEnable)
            raycaster.enabled = shouldEnable;
    }
}
