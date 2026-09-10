using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ATTACH THIS TO: the GameDirector object.
// Mode picker. Shows on Play (time + audio frozen) and again any time ESC /
// Android back is pressed while in an arena:
//   "1v1 Arena"  - the enemy car drives + shoots (EnemyDriverAI).
//   "Test Arena" - the enemy car becomes an inert TestDummy: 100 HP, never moves
//                  or shoots, still explodes with the same FX and runs the
//                  "Enemy respawns in" 5s countdown.
// While the menu is open the enemy AI is held disabled so it can't fire into the
// frozen scene. "Resume" (or ESC again) closes without switching, once a mode is
// already running.
public class ModeMenu : MonoBehaviour
{
    private enum Mode { None, Arena1v1, Test }

    private GameObject canvasGO;
    private GameObject resumeButton;
    private bool menuOpen;
    private Mode current = Mode.None;

    private static Sprite whiteSprite;

    void Awake()
    {
        Build();
        Open();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (menuOpen)
        {
            if (current != Mode.None) Close();   // ESC again = resume current mode
        }
        else
        {
            Open();
        }
    }

    // ---- open / close ----

    void Open()
    {
        menuOpen = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        SetEnemyAI(false); // hold the AI while frozen
        canvasGO.SetActive(true);
        if (resumeButton != null) resumeButton.SetActive(current != Mode.None);
    }

    void Close()
    {
        menuOpen = false;
        canvasGO.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        ApplyMode(current); // undo the temporary AI hold from Open()
    }

    void Pick(Mode mode)
    {
        current = mode;
        ApplyMode(mode);

        menuOpen = false;
        canvasGO.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    // ---- enemy configuration ----

    void SetEnemyAI(bool on)
    {
        foreach (EnemyDriverAI ai in FindObjectsByType<EnemyDriverAI>(FindObjectsSortMode.None))
        {
            if (ai.GetComponent<TestDummy>() == null) ai.enabled = on;
        }
    }

    void ApplyMode(Mode mode)
    {
        foreach (EnemyDriverAI ai in FindObjectsByType<EnemyDriverAI>(FindObjectsSortMode.None))
        {
            TestDummy dummy = ai.GetComponent<TestDummy>();

            if (mode == Mode.Test)
            {
                ai.enabled = false;
                if (dummy == null) ai.gameObject.AddComponent<TestDummy>();
            }
            else // Arena1v1 (or None -> just restore normal play)
            {
                if (dummy != null) dummy.Detach();
                ai.enabled = true;
            }
        }
    }

    // ---- runtime UI (same style as ScreenFx) ----

    void Build()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        canvasGO = new GameObject("ModeMenuCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // above ScreenFx / joysticks

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        Image bg = MakeImage("Backdrop", canvasGO.transform, new Color(0.04f, 0.04f, 0.06f, 0.94f));
        Stretch(bg.rectTransform);
        bg.raycastTarget = true; // swallow taps meant for the game / joysticks

        MakeLabel("WRAITH", canvasGO.transform, 92, FontStyle.Bold, new Vector2(0f, 240f), Color.white);
        MakeLabel("select mode", canvasGO.transform, 34, FontStyle.Normal, new Vector2(0f, 170f),
                  new Color(1f, 1f, 1f, 0.55f));

        MakeButton("1v1 Arena", canvasGO.transform, new Vector2(0f, 55f), () => Pick(Mode.Arena1v1));
        MakeButton("Test Arena", canvasGO.transform, new Vector2(0f, -105f), () => Pick(Mode.Test));
        resumeButton = MakeButton("Resume", canvasGO.transform, new Vector2(0f, -260f), () => Close());
        resumeButton.SetActive(false);
    }

    Image MakeImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = WhiteSprite();
        img.color = color;
        return img;
    }

    Text MakeLabel(string text, Transform parent, int size, FontStyle style, Vector2 pos, Color color)
    {
        GameObject go = new GameObject("Label_" + text, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Text t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = text;

        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1400f, size + 40f);
        rt.anchoredPosition = pos;
        return t;
    }

    GameObject MakeButton(string label, Transform parent, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        Image img = MakeImage("Btn_" + label, parent, new Color(0.16f, 0.17f, 0.22f, 1f));
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(560f, 130f);
        rt.anchoredPosition = pos;

        Button btn = img.gameObject.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.16f, 0.17f, 0.22f, 1f);
        cb.highlightedColor = new Color(0.26f, 0.28f, 0.36f, 1f);
        cb.pressedColor = new Color(0.10f, 0.11f, 0.14f, 1f);
        cb.selectedColor = cb.highlightedColor;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        Text t = MakeLabel(label, rt, 46, FontStyle.Bold, Vector2.zero, Color.white);
        Stretch(t.rectTransform);
        return img.gameObject;
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
}
