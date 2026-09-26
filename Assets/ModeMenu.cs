using System.Collections;
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
//   "Gridlock"          - MatchModeManager's structured mode: each team fills
//                         their own 0-100% bar just by having a car in the
//                         centre ControlZone; first to 100% wins (3.5 min
//                         timer), higher % at the buzzer wins, exact tie is a
//                         flat draw - no DeathZone for this one.
//   "Wreckoning"        - 2v2, best-of-5 rounds, entirely run by
//                         KnockoutManager on its own bigger arena - see that
//                         file for the round/forcing-zone/roster-UI rules.
//   "1v1 Arena"         - untimed free play against the live AI, no goal/HUD.
//   "Test Arena"        - the enemy car becomes an inert TestDummy: 100 HP,
//                         never moves or shoots, still explodes with the same
//                         FX and runs the "Enemy respawns in" 5s countdown.
//   "DeathZone Test"    - debug mode: drops straight into MatchModeManager's
//                         sudden death (zone shrinking immediately) against the
//                         live AI, no goal or timer - for checking out the zone
//                         without grinding out a real tie.
// Picking any of the seven doesn't start the match immediately - it fades this
// screen out and fades in a second "CHOOSE YOUR WEAPON" screen (Machine Gun /
// Rocket Launcher, see BuildWeaponMenu) while still frozen; only picking a
// weapon there applies it to the player's own Weapon and actually starts the
// match. ESC while the weapon screen is up backs out to the mode screen
// instead of resuming/closing. All seven modes run the enemy AI normally
// except Test Arena. While either screen is open the enemy AI is held
// disabled so it can't fire into the frozen scene. "Resume" (or ESC again on
// the mode screen) closes without switching, once a mode is already running.
// MatchModeManager also reopens this menu itself a few seconds after a match
// ends (Deathmatch/CTF/Gridlock/Wreckoning/DeathZone Test all count) - always
// back at the mode screen, weapon choice included.
[RequireComponent(typeof(MatchModeManager))]
public class ModeMenu : MonoBehaviour
{
    private enum Mode { None, Arena1v1, Test, Deathmatch, CaptureTheFlag, Gridlock, Wreckoning, DeathZoneTest }

    public float menuTransitionDuration = 0.28f;

    private GameObject canvasGO;
    private CanvasGroup modeGroup;
    private GameObject weaponCanvasGO;
    private CanvasGroup weaponGroup;
    private GameObject resumeButton;
    private bool menuOpen;
    private Mode current = Mode.None;
    private Mode pendingMode;
    private MatchModeManager matchModeManager;
    private Weapon playerWeapon;
    private Coroutine transitionCo;

    private static Sprite whiteSprite;

    void Awake()
    {
        matchModeManager = GetComponent<MatchModeManager>();

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) playerWeapon = playerGO.GetComponent<Weapon>();

        Build();
        BuildWeaponMenu();
        Open();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (weaponCanvasGO.activeSelf)
        {
            BackToModeSelect();
            return;
        }

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

    // Called by MatchModeManager a few seconds after a match ends.
    public void OpenMenu() => Open();

    void Open()
    {
        menuOpen = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        SetEnemyAI(false); // hold the AI while frozen

        if (transitionCo != null) { StopCoroutine(transitionCo); transitionCo = null; }
        weaponCanvasGO.SetActive(false);
        canvasGO.SetActive(true);
        modeGroup.alpha = 1f;
        if (resumeButton != null) resumeButton.SetActive(current != Mode.None);
    }

    void Close()
    {
        menuOpen = false;
        if (transitionCo != null) { StopCoroutine(transitionCo); transitionCo = null; }
        canvasGO.SetActive(false);
        weaponCanvasGO.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        ApplyMode(current); // undo the temporary AI hold from Open()
    }

    // Picking a mode doesn't start the match yet - see BuildWeaponMenu/PickWeapon.
    void Pick(Mode mode)
    {
        pendingMode = mode;
        if (transitionCo != null) StopCoroutine(transitionCo);
        transitionCo = StartCoroutine(TransitionToWeaponSelect());
    }

    IEnumerator TransitionToWeaponSelect()
    {
        yield return Fade(modeGroup, 1f, 0f, menuTransitionDuration);
        canvasGO.SetActive(false);

        weaponCanvasGO.SetActive(true);
        yield return Fade(weaponGroup, 0f, 1f, menuTransitionDuration);
        transitionCo = null;
    }

    void BackToModeSelect()
    {
        if (transitionCo != null) { StopCoroutine(transitionCo); transitionCo = null; }
        weaponCanvasGO.SetActive(false);
        weaponGroup.alpha = 0f;
        canvasGO.SetActive(true);
        modeGroup.alpha = 1f;
        if (resumeButton != null) resumeButton.SetActive(current != Mode.None);
    }

    // Real-time fade (menus run while Time.timeScale is 0).
    IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;

        group.alpha = from;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, duration > 0f ? Mathf.Clamp01(t / duration) : 1f);
            yield return null;
        }
        group.alpha = to;
    }

    // Weapon chosen - now actually start the match.
    void PickWeapon(Weapon.WeaponType type)
    {
        if (playerWeapon != null) playerWeapon.weaponType = type;

        current = pendingMode;
        ApplyMode(current);
        if (matchModeManager != null) matchModeManager.StartMatch(ToMatchMode(current));

        menuOpen = false;
        if (transitionCo != null) { StopCoroutine(transitionCo); transitionCo = null; }
        weaponCanvasGO.SetActive(false);
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
            case Mode.Gridlock: return MatchModeManager.Mode.Gridlock;
            case Mode.Wreckoning: return MatchModeManager.Mode.Wreckoning;
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

    // ---- mode-select runtime UI (same style as ScreenFx) ----

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
        modeGroup = canvasGO.AddComponent<CanvasGroup>();

        Image bg = MakeImage("Backdrop", canvasGO.transform, new Color(0.04f, 0.04f, 0.06f, 0.94f));
        Stretch(bg.rectTransform);
        bg.raycastTarget = true; // swallow taps meant for the game / joysticks

        MakeLabel("WRAITH", canvasGO.transform, 70, FontStyle.Bold, new Vector2(0f, 450f), Color.white);
        MakeLabel("select mode", canvasGO.transform, 26, FontStyle.Normal, new Vector2(0f, 396f),
                  new Color(1f, 1f, 1f, 0.55f));

        MakeButton("Deathmatch", canvasGO.transform, new Vector2(0f, 290f), () => Pick(Mode.Deathmatch));
        MakeButton("Capture the Flag", canvasGO.transform, new Vector2(0f, 194f), () => Pick(Mode.CaptureTheFlag));
        MakeButton("Gridlock", canvasGO.transform, new Vector2(0f, 98f), () => Pick(Mode.Gridlock));
        MakeButton("Wreckoning", canvasGO.transform, new Vector2(0f, 2f), () => Pick(Mode.Wreckoning));
        MakeButton("1v1 Arena", canvasGO.transform, new Vector2(0f, -94f), () => Pick(Mode.Arena1v1));
        MakeButton("Test Arena", canvasGO.transform, new Vector2(0f, -190f), () => Pick(Mode.Test));
        MakeButton("DeathZone Test", canvasGO.transform, new Vector2(0f, -286f), () => Pick(Mode.DeathZoneTest));
        resumeButton = MakeButton("Resume", canvasGO.transform, new Vector2(0f, -382f), () => Close());
        resumeButton.SetActive(false);
    }

    // ---- weapon-select runtime UI ----

    void BuildWeaponMenu()
    {
        weaponCanvasGO = new GameObject("WeaponMenuCanvas");
        weaponCanvasGO.transform.SetParent(transform, false);

        Canvas canvas = weaponCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = weaponCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        weaponCanvasGO.AddComponent<GraphicRaycaster>();
        weaponGroup = weaponCanvasGO.AddComponent<CanvasGroup>();

        Image bg = MakeImage("Backdrop", weaponCanvasGO.transform, new Color(0.04f, 0.04f, 0.06f, 0.94f));
        Stretch(bg.rectTransform);
        bg.raycastTarget = true;

        MakeLabel("CHOOSE YOUR WEAPON", weaponCanvasGO.transform, 52, FontStyle.Bold, new Vector2(0f, 340f), Color.white);

        MakeWeaponCard("Machine Gun", weaponCanvasGO.transform, new Vector2(-300f, -20f),
            BuildMachineGunIcon(), new Color(0.82f, 0.84f, 0.88f, 1f), () => PickWeapon(Weapon.WeaponType.MachineGun));
        MakeWeaponCard("Rocket Launcher", weaponCanvasGO.transform, new Vector2(300f, -20f),
            BuildRocketIcon(), new Color(1f, 0.55f, 0.2f, 1f), () => PickWeapon(Weapon.WeaponType.RocketLauncher));

        weaponCanvasGO.SetActive(false);
    }

    GameObject MakeWeaponCard(string label, Transform parent, Vector2 pos, Sprite icon, Color iconColor, UnityEngine.Events.UnityAction onClick)
    {
        Image card = MakeImage("Card_" + label, parent, new Color(0.16f, 0.17f, 0.22f, 1f));
        RectTransform rt = card.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(460f, 420f);
        rt.anchoredPosition = pos;

        Button btn = card.gameObject.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.16f, 0.17f, 0.22f, 1f);
        cb.highlightedColor = new Color(0.26f, 0.28f, 0.36f, 1f);
        cb.pressedColor = new Color(0.10f, 0.11f, 0.14f, 1f);
        cb.selectedColor = cb.highlightedColor;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(card.transform, false);
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.color = iconColor;
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;
        RectTransform irt = iconImg.rectTransform;
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(260f, 210f);
        irt.anchoredPosition = new Vector2(0f, 55f);

        MakeLabel(label, card.transform, 30, FontStyle.Bold, new Vector2(0f, -150f), Color.white);

        return card.gameObject;
    }

    // ---- shared widgets ----

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
        rt.sizeDelta = new Vector2(560f, 88f);
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

        Text t = MakeLabel(label, rt, 34, FontStyle.Bold, Vector2.zero, Color.white);
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

    // ---- weapon icons (code-generated placeholders - a real art pass will replace these) ----

    static Sprite BuildMachineGunIcon()
    {
        return BuildIconSprite((nx, ny) =>
        {
            if (InRect(nx, ny, -0.9f, 0.35f, -0.06f, 0.08f)) return true;      // barrel
            if (InRect(nx, ny, -0.86f, -0.74f, 0.08f, 0.2f)) return true;      // front sight
            if (InRect(nx, ny, -0.05f, 0.4f, -0.22f, 0.14f)) return true;      // receiver body
            if (InTriangle(nx, ny, 0.4f, 0.1f, 0.85f, -0.05f, 0.4f, -0.2f)) return true; // stock
            if (InRect(nx, ny, 0.1f, 0.24f, -0.55f, -0.2f)) return true;       // pistol grip
            if (InTriangle(nx, ny, -0.5f, -0.06f, -0.7f, -0.62f, -0.6f, -0.62f)) return true; // bipod leg 1
            if (InTriangle(nx, ny, -0.3f, -0.06f, -0.38f, -0.62f, -0.28f, -0.62f)) return true; // bipod leg 2
            return false;
        });
    }

    static Sprite BuildRocketIcon()
    {
        return BuildIconSprite((nx, ny) =>
        {
            if (InRect(nx, ny, -0.18f, 0.18f, -0.5f, 0.35f)) return true;                       // body
            if (InTriangle(nx, ny, -0.18f, 0.35f, 0.18f, 0.35f, 0f, 0.85f)) return true;         // nose cone
            if (InTriangle(nx, ny, -0.18f, -0.5f, -0.18f, -0.18f, -0.5f, -0.58f)) return true;   // left fin
            if (InTriangle(nx, ny, 0.18f, -0.5f, 0.18f, -0.18f, 0.5f, -0.58f)) return true;      // right fin
            if (InTriangle(nx, ny, -0.12f, -0.5f, 0.12f, -0.5f, 0f, -0.82f)) return true;        // exhaust flame
            return false;
        });
    }

    // 2x supersampled, then boxed down 2x2 for cheap anti-aliasing. White
    // silhouette with straight alpha - tint it via the Image's own color.
    static Sprite BuildIconSprite(System.Func<float, float, bool> inShape)
    {
        const int size = 256;
        int ss = size * 2;

        Color[] hi = new Color[ss * ss];
        for (int y = 0; y < ss; y++)
        {
            for (int x = 0; x < ss; x++)
            {
                float nx = ((x + 0.5f) / ss - 0.5f) * 2f;
                float ny = ((y + 0.5f) / ss - 0.5f) * 2f;
                hi[y * ss + x] = inShape(nx, ny) ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }

        Color[] lo = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                lo[y * size + x] = (hi[(2 * y) * ss + 2 * x] + hi[(2 * y) * ss + 2 * x + 1]
                                  + hi[(2 * y + 1) * ss + 2 * x] + hi[(2 * y + 1) * ss + 2 * x + 1]) * 0.25f;
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(lo);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool InRect(float nx, float ny, float xMin, float xMax, float yMin, float yMax)
    {
        return nx >= xMin && nx <= xMax && ny >= yMin && ny <= yMax;
    }

    static bool InTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = Cross(px - ax, py - ay, bx - ax, by - ay);
        float d2 = Cross(px - bx, py - by, cx - bx, cy - by);
        float d3 = Cross(px - cx, py - cy, ax - cx, ay - cy);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    static float Cross(float ax, float ay, float bx, float by) => ax * by - ay * bx;
}
