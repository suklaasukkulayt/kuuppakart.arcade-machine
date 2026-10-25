using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Speedometer : MonoBehaviour
{
    [Header("Source")]
    public Player player;

    [Header("Display")]
    public float speedDisplayMultiplier = 10f;
    public string unitText = "km/h";
    public float maxDisplaySpeed = 200f;
    public float tickStep = 20f;
    public float labelStep = 40f;
    public float smoothing = 10f;

    [Header("Layout")]
    public float size = 380f;
    public Vector2 margin = new Vector2(40f, 40f);
    public bool leftSide = true;

    [Header("Colors")]
    public Color trackColor = new Color(0f, 0f, 0f, 0.55f);
    public Color speedColor = new Color(0.25f, 0.85f, 1f, 1f);
    public Color speedFastColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color boostActiveColor = new Color(1f, 0.55f, 0.1f, 1f);
    public Color boostReadyColor = new Color(0.3f, 1f, 0.5f, 1f);
    public Color boostChargingColor = new Color(0.55f, 0.6f, 0.7f, 1f);
    public Color needleColor = new Color(1f, 0.25f, 0.2f, 1f);

    [Header("Optional custom assets")]
    public TMP_FontAsset font;
    public Sprite customRingSprite;

    const float StartAngle = 225f;
    const float SweepAngle = 270f;

    Canvas canvas;
    RectTransform root;
    Image speedArc;
    Image boostArc;
    RectTransform needle;
    Image needleImage;
    TextMeshProUGUI speedText;
    TextMeshProUGUI unitLabel;
    TextMeshProUGUI statusText;
    float shownSpeed01;
    float radius;

    void Start()
    {
        if (player == null)
            player = FindFirstObjectByType<Player>();

        Build();
    }

    void Build()
    {
        radius = size * 0.5f;
        Sprite ring = customRingSprite != null ? customRingSprite : MakeRingSprite(512, 0.86f);
        Sprite thinRing = customRingSprite != null ? customRingSprite : MakeRingSprite(512, 0.80f);

        GameObject cgo = new GameObject("SpeedometerCanvas");
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root = NewRect("Root", cgo.transform, new Vector2(size, size));
        float side = leftSide ? 0f : 1f;
        root.anchorMin = root.anchorMax = new Vector2(side, 0f);
        root.pivot = new Vector2(side, 0f);
        root.anchoredPosition = new Vector2(leftSide ? margin.x : -margin.x, margin.y);

        Image bg = NewImage("Backdrop", root, new Vector2(size, size), MakeDiscSprite(256), new Color(0f, 0f, 0f, 0.35f));
        bg.raycastTarget = false;

        NewArc("SpeedTrack", root, ring, size, trackColor, 1f);
        speedArc = NewArc("SpeedArc", root, ring, size, speedColor, 0f);

        float boostSize = size * 0.70f;
        NewArc("BoostTrack", root, thinRing, boostSize, trackColor, 1f);
        boostArc = NewArc("BoostArc", root, thinRing, boostSize, boostReadyColor, 1f);

        BuildTicks();

        needle = NewRect("Needle", root, new Vector2(radius * 0.80f, 5f));
        needle.pivot = new Vector2(0f, 0.5f);
        needleImage = needle.gameObject.AddComponent<Image>();
        needleImage.color = needleColor;
        needleImage.raycastTarget = false;

        Image hub = NewImage("Hub", root, new Vector2(26f, 26f), MakeDiscSprite(64), new Color(0.1f, 0.1f, 0.12f, 1f));
        hub.raycastTarget = false;

        speedText = NewText("SpeedText", root, 84, FontStyles.Bold, TextAlignmentOptions.Center);
        speedText.rectTransform.anchoredPosition = new Vector2(0f, -radius * 0.32f);
        speedText.rectTransform.sizeDelta = new Vector2(size * 0.6f, 100f);

        unitLabel = NewText("UnitText", root, 28, FontStyles.Normal, TextAlignmentOptions.Center);
        unitLabel.rectTransform.anchoredPosition = new Vector2(0f, -radius * 0.32f - 52f);
        unitLabel.rectTransform.sizeDelta = new Vector2(size * 0.4f, 40f);
        unitLabel.text = unitText;
        unitLabel.color = new Color(1f, 1f, 1f, 0.7f);

        statusText = NewText("BoostStatus", root, 30, FontStyles.Bold, TextAlignmentOptions.Center);
        statusText.rectTransform.anchoredPosition = new Vector2(0f, -radius * 0.32f - 100f);
        statusText.rectTransform.sizeDelta = new Vector2(size * 0.7f, 44f);
    }

    void Update()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
            if (player == null) return;
        }

        float displaySpeed = Mathf.Abs(player.ForwardSpeed) * speedDisplayMultiplier;
        float target01 = Mathf.Clamp01(displaySpeed / maxDisplaySpeed);
        shownSpeed01 = Mathf.Lerp(shownSpeed01, target01, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

        bool reverse = player.ForwardSpeed < -0.2f;
        bool boosted = player.IsBoosted;

        speedArc.fillAmount = shownSpeed01 * (SweepAngle / 360f);
        Color arcColor = Color.Lerp(speedColor, speedFastColor, Mathf.InverseLerp(0.5f, 1f, shownSpeed01));
        if (boosted) arcColor = boostActiveColor;
        speedArc.color = arcColor;

        float angle = StartAngle - SweepAngle * shownSpeed01;
        needle.localRotation = Quaternion.Euler(0f, 0f, angle);

        int shown = Mathf.RoundToInt(shownSpeed01 * maxDisplaySpeed);
        speedText.text = reverse ? "R " + shown : shown.ToString();
        speedText.color = boosted ? boostActiveColor : Color.white;

        if (boosted)
        {
            boostArc.fillAmount = player.BoostRemaining01 * (SweepAngle / 360f);
            boostArc.color = boostActiveColor;
            statusText.text = "BOOST!";
            statusText.color = boostActiveColor;
        }
        else if (player.BoostReady)
        {
            boostArc.fillAmount = SweepAngle / 360f;
            float pulse = 0.65f + 0.35f * Mathf.PingPong(Time.time * 2f, 1f);
            Color c = boostReadyColor; c.a = pulse;
            boostArc.color = c;
            statusText.text = "BOOST READY [B]";
            statusText.color = c;
        }
        else
        {
            boostArc.fillAmount = player.BoostCooldownProgress01 * (SweepAngle / 360f);
            boostArc.color = boostChargingColor;
            statusText.text = player.BoostCooldownLeft.ToString("F1") + "s";
            statusText.color = boostChargingColor;
        }
    }

    void BuildTicks()
    {
        for (float v = 0f; v <= maxDisplaySpeed + 0.01f; v += tickStep)
        {
            float f = v / maxDisplaySpeed;
            float ang = StartAngle - SweepAngle * f;
            float rad = ang * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            bool major = Mathf.Abs(v % labelStep) < 0.01f;

            float len = major ? 18f : 10f;
            RectTransform tick = NewRect("Tick" + v, root, new Vector2(len, major ? 4f : 2f));
            tick.anchoredPosition = dir * (radius * 0.86f - len * 0.5f - 6f);
            tick.localRotation = Quaternion.Euler(0f, 0f, ang);
            Image img = tick.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, major ? 0.9f : 0.45f);
            img.raycastTarget = false;

            if (major)
            {
                TextMeshProUGUI t = NewText("Label" + v, root, 20, FontStyles.Normal, TextAlignmentOptions.Center);
                t.rectTransform.sizeDelta = new Vector2(60f, 30f);
                t.rectTransform.anchoredPosition = dir * (radius * 0.86f - 48f);
                t.text = v.ToString("0");
                t.color = new Color(1f, 1f, 1f, 0.75f);
            }
        }
    }

    Image NewArc(string name, RectTransform parent, Sprite sprite, float arcSize, Color color, float fill)
    {
        Image img = NewImage(name, parent, new Vector2(arcSize, arcSize), sprite, color);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Bottom;
        img.fillClockwise = true;
        img.fillAmount = fill * (SweepAngle / 360f);
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(270f - StartAngle));
        img.raycastTarget = false;
        return img;
    }

    RectTransform NewRect(string name, Transform parent, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    Image NewImage(string name, RectTransform parent, Vector2 sizeDelta, Sprite sprite, Color color)
    {
        RectTransform rt = NewRect(name, parent, sizeDelta);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        return img;
    }

    TextMeshProUGUI NewText(string name, RectTransform parent, float fontSize, FontStyles style, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent, new Vector2(200f, 50f));
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.color = Color.white;
        return t;
    }

    static Sprite MakeRingSprite(int texSize, float innerFraction)
    {
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float c = (texSize - 1) * 0.5f;
        float outer = c;
        float inner = c * innerFraction;
        Color32[] px = new Color32[texSize * texSize];
        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                px[y * texSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeDiscSprite(int texSize)
    {
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float c = (texSize - 1) * 0.5f;
        Color32[] px = new Color32[texSize * texSize];
        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(c - d);
                px[y * texSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 100f);
    }
}