using UnityEngine;
using UnityEngine.EventSystems;

public class KuuppaKartTouchJoystick : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    public RectTransform handle;
    private RectTransform joystickArea;
    private KuuppaKartControls controls;
    private bool pointerActive;
    private int activePointerId;

    private void Awake()
    {
        joystickArea = GetComponent<RectTransform>();
        controls = KuuppaKartControls.EnsureInstance();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pointerActive)
            return;

        pointerActive = true;
        activePointerId = eventData.pointerId;
        UpdateJoystick(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!pointerActive || eventData.pointerId != activePointerId)
            return;

        UpdateJoystick(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pointerActive && eventData.pointerId == activePointerId)
            ReleaseJoystick();
    }

    private void UpdateJoystick(PointerEventData eventData)
    {
        if (controls == null)
            controls = KuuppaKartControls.EnsureInstance();

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickArea,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
            return;

        Rect rect = joystickArea.rect;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;

        if (radius <= 0f)
            return;

        Vector2 offset = localPoint - rect.center;
        Vector2 clampedOffset = Vector2.ClampMagnitude(offset, radius);
        Vector2 joystickValue = clampedOffset / radius;

        controls.SetOnScreenJoystick(joystickValue);

        if (handle != null)
            handle.anchoredPosition = clampedOffset;
    }

    private void ReleaseJoystick()
    {
        pointerActive = false;

        if (controls != null)
            controls.SetOnScreenJoystick(Vector2.zero);

        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }

    private void OnDisable()
    {
        ReleaseJoystick();
    }
}