using UnityEngine;
using UnityEngine.EventSystems;

public class KuuppaKartTouchButton : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    public KuuppaKartAction action;
    private KuuppaKartControls controls;
    private bool held;

    private void Awake()
    {
        controls = KuuppaKartControls.EnsureInstance();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (held)
            return;

        held = true;

        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();

        controls.SetOnScreenButton(action, true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Release();
    }

    private void OnDisable()
    {
        Release();
    }

    private void Release()
    {
        if (!held)
            return;

        held = false;

        if (controls != null)
            controls.SetOnScreenButton(action, false);
    }
}
