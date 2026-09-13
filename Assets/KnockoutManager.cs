using System.Collections;
using UnityEngine;

// ATTACH THIS TO: a dedicated "KnockoutManager" object, with its own DeathZone
// alongside (RequireComponent adds one) tuned differently to Deathmatch's -
// see the Forcing zone header - and a KnockoutHud for the round-dots/roster UI.
//
// Runs Knockout end to end once MatchModeManager.StartMatch(Knockout) calls
// BeginSeries(): a 2v2, best-of-5-rounds series with NO mid-round respawns -
// once a car is destroyed it's out until the round resets (MatchDirector's
// RespawnsSuspended is set for the whole series). A team wins a round the
// instant the whole opposing team is down; if both teams' last car goes down
// in the same instant (or same physics tick - e.g. the forcing zone's damage
// tick catching both at once), the round is a tie. First to roundsToWin (3)
// wins the series outright; if all maxRounds (5) are played without either
// side getting there, more round-wins takes it, and an equal split (tie
// rounds included, or not) makes the whole series a tie. Either way,
// MatchModeManager.EndKnockoutSeries(...) hands off to its shared
// Victory/Defeat/Draw cinematic.
//
// A round never just stalls out: this mode's own DeathZone starts closing in
// zoneStartDelay seconds into every round, its shrink speed *escalating*
// rather than constant (see Forcing zone), forcing whoever's left toward the
// middle. It runs on its own, bigger arena (see Arena header), built once at
// Awake and swapped in for the normal one only while a series is running -
// Cleanup() swaps it back out and hands control back to MatchModeManager.
[RequireComponent(typeof(DeathZone))]
[RequireComponent(typeof(KnockoutHud))]
public class KnockoutManager : MonoBehaviour
{
    public enum RoundResult { None, BlueWin, RedWin, Tie }

    [Header("Cars - Blue = Player team, Red = Enemy team (auto-found by name if left empty)")]
    public CarController blueCar1; // the Wraith (player)
    public CarController blueCar2; // AllyCar
    public CarController redCar1;  // EnemyCar
    public CarController redCar2;  // EnemyCar2

    [Header("Round-start bases (this mode's own bigger arena)")]
    public Vector3 blueBase1 = new Vector3(-4f, 0.5f, -40f);
    public Vector3 blueBase2 = new Vector3(4f, 0.5f, -40f);
    public Vector3 redBase1 = new Vector3(-4f, 0.5f, 40f);
    public Vector3 redBase2 = new Vector3(4f, 0.5f, 40f);
    public Quaternion blueBaseRotation = Quaternion.identity;                  // facing +Z, toward Red
    public Quaternion redBaseRotation = Quaternion.Euler(0f, 180f, 0f);        // facing -Z, toward Blue

    [Header("Series")]
    public int roundsToWin = 3;
    public int maxRounds = 5;
    public int preRoundCountdown = 3;  // "Round starts in: N" ticks down from this, cars locked, before it starts
    public float postRoundDelay = 3f;  // round-result banner hold before the next reset

    [Header("Forcing zone timing (this mode's own DeathZone tuning lives on that component)")]
    public float zoneStartDelay = 30f; // seconds into the round before it starts closing

    [Header("Arena (this mode's own, twice the normal size)")]
    public float planeScale = 10f;     // default Unity Plane is 10x10 units; 10 -> 100x100
    public float wallDistance = 51f;
    public float wallHeight = 6f;
    public float wallThickness = 2f;

    [Header("Team base markers - one circle per team, at the midpoint of its two cars' bases")]
    public float baseMarkerRadius = 5f; // cars sit at the edge of this, not mandatory to be inside it
    public Color blueBaseColor = new Color(0.15f, 0.4f, 0.95f, 0.28f);
    public Color redBaseColor = new Color(0.85f, 0.15f, 0.12f, 0.28f);

    private static readonly string[] OriginalArenaNames =
    {
        "Plane", "Arena_Wall_N", "Arena_Wall_S", "Arena_Wall_E", "Arena_Wall_W",
        "Cube", "Cube (1)", "Cube (2)", "Cube (3)"
    };

    private DeathZone zone;
    private KnockoutHud hud;
    private ScreenFx screenFx;
    private MatchModeManager matchModeManager;

    private GameObject[] originalArenaPieces;
    private GameObject knockoutArenaRoot;

    private Health blueHealth1, blueHealth2, redHealth1, redHealth2;
    private Respawner blueResp1, blueResp2, redResp1, redResp2;

    private int blueWins, redWins, roundsPlayed;
    private RoundResult lastRoundResult;
    private bool roundActive;
    private bool roundCheckScheduled;
    private float roundElapsed;
    private bool zoneStarted;

    void Awake()
    {
        zone = GetComponent<DeathZone>();
        hud = GetComponent<KnockoutHud>();
        screenFx = FindFirstObjectByType<ScreenFx>();
        matchModeManager = FindFirstObjectByType<MatchModeManager>();

        // AllyCar/EnemyCar2 start inactive (only this mode uses them), so the
        // lookup has to include inactive objects - a plain GameObject.Find
        // wouldn't see them yet.
        if (blueCar1 == null) blueCar1 = FindCarByName("Wraith");
        if (blueCar2 == null) blueCar2 = FindCarByName("AllyCar");
        if (redCar1 == null) redCar1 = FindCarByName("EnemyCar");
        if (redCar2 == null) redCar2 = FindCarByName("EnemyCar2");

        CacheOriginalArena();
        BuildKnockoutArena();
        SetArenaActive(false);
    }

    static CarController FindCarByName(string name)
    {
        foreach (CarController c in FindObjectsByType<CarController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.gameObject.name == name) return c;
        }
        return null;
    }

    void Update()
    {
        if (!roundActive) return;

        roundElapsed += Time.deltaTime;
        if (!zoneStarted && roundElapsed >= zoneStartDelay)
        {
            zoneStarted = true;
            zone.BeginShrinking();
            screenFx.ShowMessage("ZONE CLOSING!");
            StartCoroutine(ClearBannerAfter(2.5f));
        }
    }

    IEnumerator ClearBannerAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        screenFx.HideCountdown();
    }

    // ---- lifecycle: called by MatchModeManager ----

    public void BeginSeries()
    {
        if (blueCar1 == null || blueCar2 == null || redCar1 == null || redCar2 == null)
        {
            Debug.LogWarning("KnockoutManager: not all four cars are assigned - can't start.");
            return;
        }

        CacheHealthAndRespawners();
        SetArenaActive(true);
        blueCar2.gameObject.SetActive(true);
        redCar2.gameObject.SetActive(true);

        blueResp1.SetSpawnOverride(blueBase1, blueBaseRotation);
        blueResp2.SetSpawnOverride(blueBase2, blueBaseRotation);
        redResp1.SetSpawnOverride(redBase1, redBaseRotation);
        redResp2.SetSpawnOverride(redBase2, redBaseRotation);

        MatchDirector director = FindFirstObjectByType<MatchDirector>();
        if (director != null) director.RespawnsSuspended = true;

        Subscribe();
        hud.SetVisible(true);
        hud.ResetAll();

        blueWins = 0;
        redWins = 0;
        roundsPlayed = 0;

        StartCoroutine(RunSeries());
    }

    // Restores everything to how every other mode expects it. Safe to call
    // even if a series was never started (MatchModeManager calls this
    // unconditionally at the top of every StartMatch).
    public void Cleanup()
    {
        StopAllCoroutines();
        roundActive = false;
        roundCheckScheduled = false;

        zone.Stop();
        SetArenaActive(false);
        hud.SetVisible(false);

        Unsubscribe();

        // Blue1/Red1 (Wraith/EnemyCar) carry on into whatever mode's picked
        // next - leaving one dead/hidden because the series was abandoned
        // mid-round would strand it there, since only Knockout's own round
        // reset normally revives a car with no respawn countdown to do it.
        ClearOverrideAndRevive(blueResp1, blueHealth1);
        ClearOverrideAndRevive(blueResp2, blueHealth2);
        ClearOverrideAndRevive(redResp1, redHealth1);
        ClearOverrideAndRevive(redResp2, redHealth2);

        if (blueCar2 != null) blueCar2.gameObject.SetActive(false);
        if (redCar2 != null) redCar2.gameObject.SetActive(false);

        MatchDirector director = FindFirstObjectByType<MatchDirector>();
        if (director != null) director.RespawnsSuspended = false;
    }

    static void ClearOverrideAndRevive(Respawner r, Health h)
    {
        if (r == null) return;
        r.ClearSpawnOverride();
        if (h != null && h.IsDead) r.Respawn();
    }

    void CacheHealthAndRespawners()
    {
        blueHealth1 = blueCar1.GetComponent<Health>();
        blueHealth2 = blueCar2.GetComponent<Health>();
        redHealth1 = redCar1.GetComponent<Health>();
        redHealth2 = redCar2.GetComponent<Health>();

        blueResp1 = blueCar1.GetComponent<Respawner>();
        blueResp2 = blueCar2.GetComponent<Respawner>();
        redResp1 = redCar1.GetComponent<Respawner>();
        redResp2 = redCar2.GetComponent<Respawner>();
    }

    void Subscribe()
    {
        if (blueHealth1 != null) blueHealth1.Died += OnBlue1Died;
        if (blueHealth2 != null) blueHealth2.Died += OnBlue2Died;
        if (redHealth1 != null) redHealth1.Died += OnRed1Died;
        if (redHealth2 != null) redHealth2.Died += OnRed2Died;
    }

    void Unsubscribe()
    {
        if (blueHealth1 != null) blueHealth1.Died -= OnBlue1Died;
        if (blueHealth2 != null) blueHealth2.Died -= OnBlue2Died;
        if (redHealth1 != null) redHealth1.Died -= OnRed1Died;
        if (redHealth2 != null) redHealth2.Died -= OnRed2Died;
    }

    void OnDestroy() => Unsubscribe();

    // ---- round / series loop ----

    IEnumerator RunSeries()
    {
        while (true)
        {
            roundsPlayed++;
            yield return StartCoroutine(RunRound(roundsPlayed));

            KnockoutHud.DotResult dotResult = lastRoundResult == RoundResult.BlueWin ? KnockoutHud.DotResult.Blue
                : lastRoundResult == RoundResult.RedWin ? KnockoutHud.DotResult.Red
                : KnockoutHud.DotResult.Tie;
            hud.SetDot(roundsPlayed - 1, dotResult);

            if (lastRoundResult == RoundResult.BlueWin) blueWins++;
            else if (lastRoundResult == RoundResult.RedWin) redWins++;

            if (CheckSeriesDone(out Team? seriesWinner))
            {
                yield return new WaitForSeconds(postRoundDelay);
                matchModeManager.EndKnockoutSeries(seriesWinner);
                yield break;
            }
        }
    }

    // First to roundsToWin ends it outright. Otherwise, once every maxRounds
    // has been played, higher round-win total wins; an equal total (ties
    // included, or two even splits, or all ties - it's all just "equal") is a
    // flat series tie.
    bool CheckSeriesDone(out Team? winner)
    {
        if (blueWins >= roundsToWin) { winner = Team.Player; return true; }
        if (redWins >= roundsToWin) { winner = Team.Enemy; return true; }
        if (roundsPlayed >= maxRounds)
        {
            if (blueWins > redWins) winner = Team.Player;
            else if (redWins > blueWins) winner = Team.Enemy;
            else winner = null;
            return true;
        }
        winner = null;
        return false;
    }

    IEnumerator RunRound(int roundNumber)
    {
        ResetAllCarsForRound();
        LockAllCars(true);

        for (int n = Mathf.Max(1, preRoundCountdown); n >= 1; n--)
        {
            screenFx.ShowCountdown("Round starts in:", n);
            yield return new WaitForSeconds(1f);
        }
        screenFx.HideCountdown();
        LockAllCars(false);

        lastRoundResult = RoundResult.None;
        roundElapsed = 0f;
        zoneStarted = false;
        zone.Stop();
        roundActive = true;

        yield return new WaitUntil(() => !roundActive);

        zone.Stop();
        LockAllCars(true);

        string label = lastRoundResult == RoundResult.BlueWin ? "BLUE WINS ROUND " + roundNumber
            : lastRoundResult == RoundResult.RedWin ? "RED WINS ROUND " + roundNumber
            : "ROUND " + roundNumber + " - TIE";
        screenFx.ShowMessage(label);

        yield return new WaitForSeconds(postRoundDelay);
        screenFx.HideCountdown();
    }

    void ResetAllCarsForRound()
    {
        hud.SetEliminated(0, false);
        hud.SetEliminated(1, false);
        hud.SetEliminated(2, false);
        hud.SetEliminated(3, false);

        if (blueResp1 != null) blueResp1.Respawn();
        if (blueResp2 != null) blueResp2.Respawn();
        if (redResp1 != null) redResp1.Respawn();
        if (redResp2 != null) redResp2.Respawn();
    }

    void LockAllCars(bool locked)
    {
        LockCar(blueCar1, locked);
        LockCar(blueCar2, locked);
        LockCar(redCar1, locked);
        LockCar(redCar2, locked);
    }

    static void LockCar(CarController car, bool locked)
    {
        if (car == null) return;
        car.inputLocked = locked;
        Weapon w = car.GetComponent<Weapon>();
        if (w != null) w.inputLocked = locked;
    }

    // ---- death handling ----
    // Each car has its own handler (rather than one generic one) so the right
    // roster slot gets its elimination cross with no lookup needed.

    void OnBlue1Died(Health h) { hud.SetEliminated(0, true); ScheduleRoundCheck(); }
    void OnBlue2Died(Health h) { hud.SetEliminated(1, true); ScheduleRoundCheck(); }
    void OnRed1Died(Health h) { hud.SetEliminated(2, true); ScheduleRoundCheck(); }
    void OnRed2Died(Health h) { hud.SetEliminated(3, true); ScheduleRoundCheck(); }

    // Deferred by one frame: if the forcing zone's damage tick (or anything
    // else) kills more than one car in the same update, this only resolves
    // once every one of THIS frame's deaths has actually landed - otherwise a
    // genuine double-KO could get misread as a win for whichever car's Died
    // event happened to fire first.
    void ScheduleRoundCheck()
    {
        if (roundCheckScheduled) return;
        roundCheckScheduled = true;
        StartCoroutine(CheckRoundEndNextFrame());
    }

    IEnumerator CheckRoundEndNextFrame()
    {
        yield return null;
        roundCheckScheduled = false;
        EvaluateRoundEnd();
    }

    void EvaluateRoundEnd()
    {
        if (!roundActive) return;

        bool blueAlive = IsAlive(blueHealth1) || IsAlive(blueHealth2);
        bool redAlive = IsAlive(redHealth1) || IsAlive(redHealth2);
        if (blueAlive && redAlive) return;

        if (!blueAlive && !redAlive) lastRoundResult = RoundResult.Tie;
        else if (blueAlive) lastRoundResult = RoundResult.BlueWin;
        else lastRoundResult = RoundResult.RedWin;

        roundActive = false;
    }

    static bool IsAlive(Health h) => h != null && !h.IsDead;

    // ---- arena swap ----

    void CacheOriginalArena()
    {
        originalArenaPieces = new GameObject[OriginalArenaNames.Length];
        for (int i = 0; i < OriginalArenaNames.Length; i++)
        {
            originalArenaPieces[i] = GameObject.Find(OriginalArenaNames[i]);
        }
    }

    void BuildKnockoutArena()
    {
        knockoutArenaRoot = new GameObject("KnockoutArena");

        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "KnockoutPlane";
        plane.transform.SetParent(knockoutArenaRoot.transform, false);
        plane.transform.localScale = new Vector3(planeScale, 1f, planeScale);

        float wallSpan = wallDistance * 2f + 4f; // small overlap so corners fully seal
        BuildWall("N", new Vector3(0f, wallHeight * 0.5f, wallDistance), new Vector3(wallSpan, wallHeight, wallThickness));
        BuildWall("S", new Vector3(0f, wallHeight * 0.5f, -wallDistance), new Vector3(wallSpan, wallHeight, wallThickness));
        BuildWall("E", new Vector3(wallDistance, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, wallSpan));
        BuildWall("W", new Vector3(-wallDistance, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, wallSpan));

        BuildBaseMarker("BlueBase", Vector3.Lerp(blueBase1, blueBase2, 0.5f), blueBaseColor);
        BuildBaseMarker("RedBase", Vector3.Lerp(redBase1, redBase2, 0.5f), redBaseColor);
    }

    void BuildBaseMarker(string markerName, Vector3 pos, Color color)
    {
        GameObject go = new GameObject(markerName);
        go.SetActive(false); // configure BaseMarker's fields before its Awake() builds the visual
        go.transform.SetParent(knockoutArenaRoot.transform, false);
        go.transform.position = pos;

        BaseMarker marker = go.AddComponent<BaseMarker>();
        marker.radius = baseMarkerRadius;
        marker.color = color;

        go.SetActive(true);
    }

    void BuildWall(string dir, Vector3 pos, Vector3 size)
    {
        GameObject wall = new GameObject("KnockoutWall_" + dir);
        wall.transform.SetParent(knockoutArenaRoot.transform, false);
        wall.transform.position = pos;
        BoxCollider col = wall.AddComponent<BoxCollider>();
        col.size = size;

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "KnockoutWallPanel_" + dir;
        Destroy(panel.GetComponent<Collider>());
        panel.transform.SetParent(knockoutArenaRoot.transform, false);
        panel.transform.position = pos;
        panel.transform.localScale = size;

        MeshRenderer mr = panel.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mr.material = new Material(shader) { color = new Color(0.35f, 0.35f, 0.4f) };
    }

    void SetArenaActive(bool useKnockoutArena)
    {
        if (knockoutArenaRoot != null) knockoutArenaRoot.SetActive(useKnockoutArena);
        foreach (GameObject go in originalArenaPieces)
        {
            if (go != null) go.SetActive(!useKnockoutArena);
        }
    }
}
