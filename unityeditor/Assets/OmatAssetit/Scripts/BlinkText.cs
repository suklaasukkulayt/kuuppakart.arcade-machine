using UnityEngine;
using TMPro;

public class BlinkText : MonoBehaviour
{
    [Header("Blinking settings")]
    public TMP_Text text;
    public string message = "Press any button to start";
    public float interval = 1f;
    private bool showing = false;

    void OnEnable()
    {
        InvokeRepeating(nameof(ToggleText), 0f, interval);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(ToggleText));
    }

    void ToggleText()
    {
        showing = !showing;
        text.text = showing ? message : "";
    }
}