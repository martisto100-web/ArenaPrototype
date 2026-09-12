using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ATTACH THIS TO: the GameDirector object.
// Mode picker. Shows on Play (time + audio frozen) and again any time ESC /
// Android back is pressed while in an arena:
//   "Deathmatch"        - MatchModeManager's structured mode: first to 10 kills
//                         (3 min timer), tiebreak -> DeathZone sudden death.
//   "Capture the Flag"  - MatchModeManager's structured mode: first to 3 flag
//                         captures (4 min timer), tiebreak on kills then
//                         DeathZone sudden death.
//   "Zone Control"      - MatchModeManager's structured mode (working name):
//                         each team fills their own 0-100% bar just by having
//                         a car in the centre ControlZone; first to 100% wins
//                         (3.5 min timer), higher % at the buzzer wins, exact
//                         tie is a flat draw - no DeathZone for this one.
//   "1v1 Arena"         - untimed free play against the live AI, no goal/HUD.
//   "Test Arena"        - the enemy car becomes an inert TestDummy: 100 HP,
//                         never moves or shoots, still explodes with the same
//                         FX and runs the "Enemy respawns in" 5s countdown.
//   "DeathZone Test"    - debug mode: drops straight into MatchModeManager's
//                         sudden death (zone shrinking immediately) against the
//                         live AI, no goal/timer - for checking out the zone
//                         without grinding out a real tie.
// All six run the enemy AI normally except Test Arena. While the menu is open
// the enemy AI is held disabled so it can't fire into the frozen scene.
// "Resume" (or ESC again) closes without switching, once a mode is already
// running. MatchModeManager also reopens this menu itself a few seconds after
// a match ends (Deathmatch/CTF/Zone Control/DeathZone Test all count).
[RequireComponent(typeof(MatchModeManager))]
public class ModeMenu : MonoBehaviour
{
    private enum Mode { None, Arena1v1, Test, Deathmatch, CaptureTheFlag, ZoneControl, DeathZoneTest }

    private GameObject canvasGO;
    private GameObject resumeButton;
    private bool menuOpen;
    private Mode current = Mode.None;
    private MatchModeManager matchModeManager;

    private static Sprite whiteSprite;

    void Awake()
    {
        matchModeManager = GetComponent<MatchModeManager>();
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

    // Called by MatchModeManager a few seconds after a Deathmatch/CTF match ends.
    public void OpenMenu() => Open();

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
        if (matchModeManager != null) matchModeManager.StartMatch(ToMatchMode(mode));

        menuOpen = false;
        canvasGO.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    static MatchModeManager.Mode ToMatchMode(Mode mode)
    {
        switch (mode)
        {
            case Mode.Deathmatch: return MatchModeManager.Mode.Deathmatch;
            case Mode.CaptureTheFlag: return MatchModeManager.Mode.CaptureTheFlag;
            case Mode.ZoneControl: return MatchModeManager.Mode.ZoneControl;
            case Mode.DeathZoneTest: return MatchModeManager.Mode.DeathZoneTest;
            default: return MatchModeManager.Mode.None; // Arena1v1 / Test - free play, no structured match
        }
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

        MakeLabel("WRAITH", canvasGO.transform, 80, FontStyle.Bold, new Vector2(0f, 420f), Color.white);
        MakeLabel("select mode", canvasGO.transform, 30, FontStyle.Normal, new Vector2(0f, 362f),
                  new Color(1f, 1f, 1f, 0.55f));

        MakeButton("Deathmatch", canvasGO.transform, new Vector2(0f, 260f), () => Pick(Mode.Deathmatch));
        MakeButton("Capture the Flag", canvasGO.transform, new Vector2(0f, 148f), () => Pick(Mode.CaptureTheFlag));
        MakeButton("Zone Control", canvasGO.transform, new Vector2(0f, 36f), () => Pick(Mode.ZoneControl));
        MakeButton("1v1 Arena", canvasGO.transform, new Vector2(0f, -76f), () => Pick(Mode.Arena1v1));
        MakeButton("Test Arena", canvasGO.transform, new Vector2(0f, -188f), () => Pick(Mode.Test));
        MakeButton("DeathZone Test", canvasGO.transform, new Vector2(0f, -300f), () => Pick(Mode.DeathZoneTest));
        resumeButton = MakeButton("Resume", canvasGO.transform, new Vector2(0f, -412f), () => Close());
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
        rt.sizeDelta = new Vector2(560f, 100f);
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

        Text t = MakeLabel(label, rt, 38, FontStyle.Bold, Vector2.zero, Color.white);
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
