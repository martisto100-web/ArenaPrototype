using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ATTACH THIS TO: the GameDirector object (alongside MatchDirector).
// Builds a screen-space overlay at runtime: a full-screen flash, a centred
// countdown/banner label, and a small top-of-screen HUD line (used by
// MatchModeManager for the Deathmatch / Capture the Flag score + timer). No
// scene or prefab setup needed.
public class ScreenFx : MonoBehaviour
{
    [Header("Flash")]
    public Color flashColor = new Color(1f, 0.9f, 0.75f, 1f);
    [Range(0f, 1f)] public float flashPeakAlpha = 0.6f;
    public float flashFade = 0.35f;

    [Header("Countdown label")]
    public int fontSize = 46;
    public Color textColor = Color.white;

    [Header("HUD label")]
    public int hudFontSize = 34;

    private Image flash;
    private Text countdown;
    private Text hud;
    private Coroutine flashCo;

    private static Sprite whiteSprite;

    void Awake()
    {
        Build();
    }

    void Build()
    {
        GameObject canvasGO = new GameObject("ScreenFxCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject flashGO = new GameObject("Flash");
        flashGO.transform.SetParent(canvasGO.transform, false);
        flash = flashGO.AddComponent<Image>();
        flash.sprite = WhiteSprite();
        flash.raycastTarget = false;
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        RectTransform fr = flash.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = Vector2.zero;
        fr.offsetMax = Vector2.zero;

        GameObject textGO = new GameObject("Countdown");
        textGO.transform.SetParent(canvasGO.transform, false);
        countdown = textGO.AddComponent<Text>();
        countdown.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        countdown.fontSize = fontSize;
        countdown.fontStyle = FontStyle.Bold;
        countdown.alignment = TextAnchor.MiddleCenter;
        countdown.color = textColor;
        countdown.raycastTarget = false;
        countdown.horizontalOverflow = HorizontalWrapMode.Overflow;
        countdown.verticalOverflow = VerticalWrapMode.Overflow;
        countdown.text = string.Empty;
        RectTransform tr = countdown.rectTransform;
        tr.anchorMin = new Vector2(0.5f, 0.5f);
        tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.sizeDelta = new Vector2(1200f, 220f);
        tr.anchoredPosition = new Vector2(0f, 170f);

        Shadow shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(2f, -2f);

        GameObject hudGO = new GameObject("Hud");
        hudGO.transform.SetParent(canvasGO.transform, false);
        hud = hudGO.AddComponent<Text>();
        hud.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hud.fontSize = hudFontSize;
        hud.fontStyle = FontStyle.Bold;
        hud.alignment = TextAnchor.UpperCenter;
        hud.color = Color.white;
        hud.raycastTarget = false;
        hud.horizontalOverflow = HorizontalWrapMode.Overflow;
        hud.verticalOverflow = VerticalWrapMode.Overflow;
        hud.text = string.Empty;
        RectTransform hr = hud.rectTransform;
        hr.anchorMin = new Vector2(0.5f, 1f);
        hr.anchorMax = new Vector2(0.5f, 1f);
        hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(1400f, 100f);
        hr.anchoredPosition = new Vector2(0f, -20f);

        Shadow hudShadow = hudGO.AddComponent<Shadow>();
        hudShadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        hudShadow.effectDistance = new Vector2(2f, -2f);
    }

    static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D tex = new Texture2D(2, 2);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }

    public void Flash()
    {
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        float t = flashFade;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            float k = flashFade > 0f ? Mathf.Clamp01(t / flashFade) : 0f;
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, k * flashPeakAlpha);
            yield return null;
        }
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
    }

    public void ShowCountdown(string label, int seconds)
    {
        if (countdown != null) countdown.text = label + "  " + seconds;
    }

    // Plain centred banner text, no trailing number - "SUDDEN DEATH!", "YOU WIN".
    public void ShowMessage(string text)
    {
        if (countdown != null) countdown.text = text;
    }

    public void HideCountdown()
    {
        if (countdown != null) countdown.text = string.Empty;
    }

    // Persistent top-of-screen line - MatchModeManager's score/timer readout.
    public void SetHud(string text)
    {
        if (hud != null) hud.text = text;
    }

    public void HideHud()
    {
        if (hud != null) hud.text = string.Empty;
    }
}
