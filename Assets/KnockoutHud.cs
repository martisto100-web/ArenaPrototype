using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ATTACH THIS TO: the same object as KnockoutManager.
// Builds Knockout's own runtime UI at Awake (no art, same approach as
// ScreenFx/ModeMenu) and stays hidden until KnockoutManager calls
// SetVisible(true) for the length of a series:
//   - Five round-result dots, upper-middle - SetDot(i, result) fills dot i in
//     blue/red/tie as each round of the best-of-5 concludes.
//   - Two small roster squares upper-left (Blue team: "Blue 1"/"Blue 2") and
//     two upper-right (Red team) - a stand-in for real portraits until a
//     design pass. SetEliminated(slot, true) gently scales/fades a red "X"
//     over that player's square; false clears it (a round reset).
// Slot order for the roster methods: 0 = Blue 1, 1 = Blue 2, 2 = Red 1, 3 = Red 2.
public class KnockoutHud : MonoBehaviour
{
    public enum DotResult { Empty, Blue, Red, Tie }

    [Header("Round dots")]
    public int dotCount = 5;
    public float dotSize = 26f;
    public float dotSpacing = 14f;
    public Color dotEmptyColor = new Color(0.4f, 0.4f, 0.45f, 0.6f);
    public Color dotBlueColor = new Color(0.25f, 0.5f, 1f, 1f);
    public Color dotRedColor = new Color(0.95f, 0.25f, 0.2f, 1f);
    public Color dotTieColor = new Color(0.85f, 0.8f, 0.35f, 1f);

    [Header("Roster panels")]
    public Vector2 panelSize = new Vector2(130f, 60f);
    public float panelMargin = 24f;
    public float panelSpacing = 10f;
    public Color blueBorder = new Color(0.25f, 0.5f, 1f, 1f);
    public Color redBorder = new Color(0.95f, 0.25f, 0.2f, 1f);

    [Header("Elimination animation")]
    public float crossPopDuration = 0.45f;

    private GameObject canvasGO;
    private Image[] dots;
    private Text[] crosses;
    private Coroutine[] crossCo;

    private static Sprite whiteSprite;
    private static Sprite dotSprite;

    void Awake()
    {
        Build();
        SetVisible(false);
    }

    public void SetVisible(bool on)
    {
        if (canvasGO != null) canvasGO.SetActive(on);
    }

    // Blanks every dot and clears every elimination cross - called at the
    // start of a fresh series.
    public void ResetAll()
    {
        for (int i = 0; i < dots.Length; i++) dots[i].color = dotEmptyColor;
        for (int i = 0; i < crosses.Length; i++) SetEliminatedImmediate(i, false);
    }

    public void SetDot(int index, DotResult result)
    {
        if (dots == null || index < 0 || index >= dots.Length) return;
        Color c;
        switch (result)
        {
            case DotResult.Blue: c = dotBlueColor; break;
            case DotResult.Red: c = dotRedColor; break;
            case DotResult.Tie: c = dotTieColor; break;
            default: c = dotEmptyColor; break;
        }
        dots[index].color = c;
    }

    public void SetEliminated(int slot, bool eliminated)
    {
        if (crosses == null || slot < 0 || slot >= crosses.Length) return;
        if (crossCo[slot] != null) StopCoroutine(crossCo[slot]);

        if (eliminated) crossCo[slot] = StartCoroutine(PopCross(slot));
        else SetEliminatedImmediate(slot, false);
    }

    void SetEliminatedImmediate(int slot, bool eliminated)
    {
        Text t = crosses[slot];
        if (t == null) return;
        t.gameObject.SetActive(eliminated);
        t.rectTransform.localScale = Vector3.one;
        Color c = t.color;
        c.a = eliminated ? 1f : 0f;
        t.color = c;
    }

    // Gentle pop: scales up from small + fades in, ease-out.
    IEnumerator PopCross(int slot)
    {
        Text t = crosses[slot];
        t.gameObject.SetActive(true);
        float time = 0f;
        while (time < crossPopDuration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / crossPopDuration);
            float eased = 1f - (1f - k) * (1f - k);
            t.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, eased);
            Color c = t.color;
            c.a = eased;
            t.color = c;
            yield return null;
        }
        t.rectTransform.localScale = Vector3.one;
        Color final = t.color;
        final.a = 1f;
        t.color = final;
    }

    // ---- build ----

    void Build()
    {
        canvasGO = new GameObject("KnockoutHudCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildDots();
        BuildPanels();
    }

    void BuildDots()
    {
        dots = new Image[Mathf.Max(0, dotCount)];
        float totalWidth = dots.Length * dotSize + (dots.Length - 1) * dotSpacing;
        float startX = -totalWidth * 0.5f + dotSize * 0.5f;

        for (int i = 0; i < dots.Length; i++)
        {
            GameObject go = new GameObject("Dot" + i, typeof(RectTransform));
            go.transform.SetParent(canvasGO.transform, false);

            Image img = go.AddComponent<Image>();
            img.sprite = DotSprite();
            img.color = dotEmptyColor;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(dotSize, dotSize);
            rt.anchoredPosition = new Vector2(startX + i * (dotSize + dotSpacing), -20f);

            dots[i] = img;
        }
    }

    void BuildPanels()
    {
        crosses = new Text[4];
        crossCo = new Coroutine[4];

        BuildPanel(0, "Blue 1", blueBorder, true, 0);
        BuildPanel(1, "Blue 2", blueBorder, true, 1);
        BuildPanel(2, "Red 1", redBorder, false, 0);
        BuildPanel(3, "Red 2", redBorder, false, 1);
    }

    void BuildPanel(int index, string label, Color borderColor, bool leftSide, int stackIndex)
    {
        GameObject root = new GameObject("Panel_" + label, typeof(RectTransform));
        root.transform.SetParent(canvasGO.transform, false);

        Image bg = root.AddComponent<Image>();
        bg.sprite = WhiteSprite();
        bg.color = new Color(0.05f, 0.05f, 0.07f, 0.75f);

        RectTransform rt = bg.rectTransform;
        float xSign = leftSide ? 1f : -1f;
        rt.anchorMin = rt.anchorMax = new Vector2(leftSide ? 0f : 1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = panelSize;
        rt.anchoredPosition = new Vector2(
            xSign * (panelMargin + panelSize.x * 0.5f),
            -(panelMargin + stackIndex * (panelSize.y + panelSpacing) + panelSize.y * 0.5f));

        // Thin coloured border: a slightly larger Image behind the background.
        GameObject borderGO = new GameObject("Border", typeof(RectTransform));
        borderGO.transform.SetParent(root.transform, false);
        borderGO.transform.SetAsFirstSibling();
        Image border = borderGO.AddComponent<Image>();
        border.sprite = WhiteSprite();
        border.color = borderColor;
        RectTransform brt = border.rectTransform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-3f, -3f);
        brt.offsetMax = new Vector2(3f, 3f);

        Text nameLabel = MakeLabel(label, root.transform, 22, Color.white);
        Stretch(nameLabel.rectTransform);

        Text cross = MakeLabel("X", root.transform, 46, new Color(0.95f, 0.15f, 0.1f, 0f));
        Stretch(cross.rectTransform);
        cross.gameObject.SetActive(false);
        crosses[index] = cross;
    }

    Text MakeLabel(string text, Transform parent, int size, Color color)
    {
        GameObject go = new GameObject("Label_" + text, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Text t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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

    static Sprite DotSprite()
    {
        if (dotSprite != null) return dotSprite;
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (size * 0.5f);
                float a = Mathf.Clamp01((1f - d) * 4f); // small soft anti-aliased rim
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        dotSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return dotSprite;
    }
}
