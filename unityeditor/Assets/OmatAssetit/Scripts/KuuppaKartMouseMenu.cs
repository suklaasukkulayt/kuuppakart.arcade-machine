using UnityEngine;
using UnityEngine.UI;

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

    private void LateUpdate()
    {
        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();
        bool shouldEnable = controls.MouseMenuInputEnabled;
        if (raycaster.enabled != shouldEnable)
            raycaster.enabled = shouldEnable;
    }
}