using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ATTACH THIS TO: the GameDirector object (alongside ModeMenu / ScreenFx).
// Runs the four structured modes ModeMenu can start:
//   Deathmatch        - first team to killGoal eliminations wins; 5 min timer.
//   Capture the Flag  - first team to captureGoal flag captures wins; 4 min timer.
//   Gridlock          - first team to fill their OWN 0-100% bar to 100% wins;
//                       3.5 min timer.
//   Knockout          - 2v2, best-of-5 rounds, entirely owned by
//                       KnockoutManager (its own arena, forcing zone, roster
//                       and round-dot UI) - see that file. This class just
//                       starts/stops it and relays its final result into the
//                       same end-of-match cinematic every other mode uses.
// In Deathmatch/CTF, if nobody hits the goal before the timer runs out the team
// with more eliminations wins (captures are CTF's win condition, kills are just
// its tiebreaker); if THAT'S also tied, DeathZone sudden death decides it -
// first car to go down loses. Gridlock has no tiebreaker: nobody at 100% at the
// buzzer just means whoever has the higher percentage wins outright, and an
// exact tie is a flat draw - it never hands off to DeathZone. Also runs
// DeathZoneTest - a debug-only mode (ModeMenu's "DeathZone Test" button) that
// drops straight into sudden death with no goal or timer, so the zone can be
// checked out without grinding out a real tie.
// Kills/respawns themselves are still all MatchDirector / Respawner as always;
// this only watches the score and owns the HUD text (drawn through ScreenFx),
// Flag setup for CTF, reading ControlZone occupancy for Gridlock, and the
// post-match cinematic every mode ends with: a "VICTORY"/"DEFEAT"/"DRAW"
// banner (from the local player's own point of view - there's no networked
// opponent to word it neutrally for yet), controls locked on every car
// currently in play (2, or Knockout's 4) so they coast to a natural stop and
// the camera eases back for a wider view, then a beat of quiet before the
// mode-select menu reopens (standing in for a proper post-match/loading flow,
// which doesn't exist yet).
[RequireComponent(typeof(ScreenFx))]
[RequireComponent(typeof(DeathZone))]
public class MatchModeManager : MonoBehaviour
{
    public enum Mode { None, Deathmatch, CaptureTheFlag, Gridlock, Knockout, DeathZoneTest }

    [Header("Deathmatch")]
    public int deathmatchKillGoal = 10;
    public float deathmatchDuration = 300f;

    [Header("Capture The Flag")]
    public int ctfCaptureGoal = 3;
    public float ctfDuration = 240f;

    [Header("Gridlock")]
    public float gridlockDuration = 210f;         // 3.5 minutes
    public float gridlockSecondsPerPercent = 0.7f; // 1% per this many seconds a team holds the zone alone or contested

    [Header("Post-match sequence")]
    public float postMatchZoomDuration = 1.5f; // how long the camera's pull-back takes
    public float postMatchMenuDelay = 2.5f;    // the quiet beat after everything settles, before the menu reopens
    public float postMatchMaxWait = 6f;        // safety cap - see Respawner.maxFallTime for the same idea

    public Mode CurrentMode { get; private set; } = Mode.None;

    private ScreenFx screenFx;
    private DeathZone deathZone;
    private ModeMenu modeMenu;
    private MatchDirector matchDirector;
    private CameraFollow cameraFollow;
    private KnockoutManager knockoutManager;

    private Health playerHealth;
    private Health enemyHealth;
    private Flag playerFlag;        // the Player team's own flag/base
    private Flag enemyFlag;         // the Enemy team's own flag/base
    private ControlZone controlZone;

    // Every currently-active car/weapon/AI, regardless of mode - 2 for
    // everything but Knockout's 4. The shared end-of-match cinematic locks
    // and settle-checks whatever's in these, so it scales to either roster
    // size with no special-casing.
    private readonly List<CarController> allCars = new List<CarController>();
    private readonly List<Weapon> allWeapons = new List<Weapon>();
    private readonly List<EnemyDriverAI> allAI = new List<EnemyDriverAI>();

    private int playerKills;
    private int enemyKills;
    private int playerCaptures;
    private int enemyCaptures;
    private float playerZonePercent;
    private float enemyZonePercent;
    private float timeRemaining;
    private bool matchRunning;
    private bool suddenDeath;

    void Awake()
    {
        screenFx = GetComponent<ScreenFx>();
        deathZone = GetComponent<DeathZone>();
        modeMenu = GetComponent<ModeMenu>();
        matchDirector = GetComponent<MatchDirector>();
        knockoutManager = FindFirstObjectByType<KnockoutManager>();
        if (Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();
    }

    void OnDestroy() => Unsubscribe();

    // Rebuilds the roster from every currently-ACTIVE TeamMember - call this
    // again after activating/deactivating cars (e.g. once Knockout's extra two
    // are switched on) so the roster reflects who's actually in play.
    void FindCombatants()
    {
        playerHealth = null;
        enemyHealth = null;
        allCars.Clear();
        allWeapons.Clear();
        allAI.Clear();

        foreach (TeamMember tm in FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            Health h = tm.GetComponent<Health>();
            if (h == null) continue;

            CarController car = tm.GetComponent<CarController>();
            Weapon weapon = tm.GetComponent<Weapon>();
            EnemyDriverAI ai = tm.GetComponent<EnemyDriverAI>();
            if (car != null) allCars.Add(car);
            if (weapon != null) allWeapons.Add(weapon);
            if (ai != null) allAI.Add(ai);

            // "Primary" per team, for the 2-car modes' kill-counting/HUD -
            // whichever is found first when there's more than one (Knockout
            // doesn't use these at all, so which one hardly matters there).
            if (tm.team == Team.Player)
            {
                if (playerHealth == null) playerHealth = h;
            }
            else
            {
                if (enemyHealth == null) enemyHealth = h;
            }
        }

        playerFlag = null;
        enemyFlag = null;
        foreach (Flag f in FindObjectsByType<Flag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (f.owningTeam == Team.Player) playerFlag = f; else enemyFlag = f;
        }
        if (playerFlag != null) playerFlag.SetOther(enemyFlag);
        if (enemyFlag != null) enemyFlag.SetOther(playerFlag);

        controlZone = FindFirstObjectByType<ControlZone>(FindObjectsInactive.Include);
    }

    // ---- called by ModeMenu on every mode pick (including switching to 1v1 / Test / None) ----

    public void StartMatch(Mode mode)
    {
        StopAllCoroutines();
        Unsubscribe();
        FindCombatants();

        // A new match always starts with full control and normal framing,
        // regardless of what state the last match's end sequence left things in.
        LockControls(false);
        if (cameraFollow != null) cameraFollow.ResetZoomImmediate();
        // Always restore Knockout's arena/extra cars/suspended-respawns first -
        // safe even if a series wasn't running - so leaving that mode never
        // leaves anything behind for whatever's picked next.
        if (knockoutManager != null) knockoutManager.Cleanup();

        CurrentMode = mode;
        matchRunning = mode != Mode.None;
        suddenDeath = false;
        playerKills = enemyKills = playerCaptures = enemyCaptures = 0;
        playerZonePercent = enemyZonePercent = 0f;
        deathZone.Stop();

        SetFlagsActive(mode == Mode.CaptureTheFlag);
        SetZoneActive(mode == Mode.Gridlock);

        if (!matchRunning)
        {
            screenFx.HideHud();
            return;
        }

        if (mode == Mode.Knockout)
        {
            if (knockoutManager != null)
            {
                knockoutManager.BeginSeries();
                FindCombatants(); // rebuild the roster now that the extra two cars are active
            }
            return; // KnockoutManager owns the score, HUD and timing from here
        }

        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        if (enemyHealth != null) enemyHealth.Died += OnEnemyDied;
        if (mode == Mode.CaptureTheFlag)
        {
            if (playerFlag != null) playerFlag.Captured += OnCaptured;
            if (enemyFlag != null) enemyFlag.Captured += OnCaptured;
        }

        if (mode == Mode.DeathZoneTest)
        {
            // Skip straight to the exact state a real tie-at-the-buzzer leaves
            // you in - no goal, no timer, just the zone and whoever's left.
            timeRemaining = 0f;
            suddenDeath = true;
            deathZone.BeginShrinking();
            screenFx.ShowMessage("SUDDEN DEATH!");
            StartCoroutine(ClearBannerAfter(2.5f));
        }
        else
        {
            switch (mode)
            {
                case Mode.Deathmatch: timeRemaining = deathmatchDuration; break;
                case Mode.CaptureTheFlag: timeRemaining = ctfDuration; break;
                case Mode.Gridlock: timeRemaining = gridlockDuration; break;
                default: timeRemaining = 0f; break;
            }
        }

        UpdateHud();
    }

    // Called by KnockoutManager once its best-of-5 series concludes - reuses
    // the exact same Victory/Defeat/Draw cinematic every other mode ends with.
    public void EndKnockoutSeries(Team? winner)
    {
        if (winner.HasValue) EndMatch(winner.Value);
        else EndMatchDraw();
    }

    void SetFlagsActive(bool on)
    {
        if (playerFlag != null) playerFlag.gameObject.SetActive(on);
        if (enemyFlag != null) enemyFlag.gameObject.SetActive(on);
    }

    void SetZoneActive(bool on)
    {
        if (controlZone != null) controlZone.gameObject.SetActive(on);
    }

    void Unsubscribe()
    {
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        if (enemyHealth != null) enemyHealth.Died -= OnEnemyDied;
        if (playerFlag != null) playerFlag.Captured -= OnCaptured;
        if (enemyFlag != null) enemyFlag.Captured -= OnCaptured;
    }

    // ---- per-frame ----

    void Update()
    {
        if (!matchRunning) return;
        if (CurrentMode == Mode.Knockout) return; // KnockoutManager runs its own loop entirely

        if (CurrentMode == Mode.Gridlock)
        {
            TickGridlock();
            if (!matchRunning) return; // a team may have just hit 100%
        }

        if (!suddenDeath)
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                if (CurrentMode == Mode.Gridlock) ResolveGridlockAtBuzzer();
                else ResolveAtBuzzer();
            }
        }
        UpdateHud();
    }

    // Each team's bar fills at its own pace - 1% per gridlockSecondsPerPercent
    // while at least one of their cars is in the zone - completely independent
    // of whether the enemy is also inside; both can climb at once.
    void TickGridlock()
    {
        if (controlZone == null) return;

        float gain = Time.deltaTime / Mathf.Max(0.01f, gridlockSecondsPerPercent);
        if (controlZone.PlayerInside) playerZonePercent = Mathf.Min(100f, playerZonePercent + gain);
        if (controlZone.EnemyInside) enemyZonePercent = Mathf.Min(100f, enemyZonePercent + gain);

        if (playerZonePercent >= 100f) { EndMatch(Team.Player); return; }
        if (enemyZonePercent >= 100f) { EndMatch(Team.Enemy); return; }
    }

    // ---- events ----

    void OnPlayerDied(Health h)
    {
        // Whatever flag this car was holding heads straight home.
        if (enemyFlag != null) enemyFlag.DropIfCarriedBy(h.transform);
        if (playerFlag != null) playerFlag.DropIfCarriedBy(h.transform);

        if (suddenDeath) { EndMatch(Team.Enemy); return; }
        enemyKills++;
        CheckGoal();
    }

    void OnEnemyDied(Health h)
    {
        if (enemyFlag != null) enemyFlag.DropIfCarriedBy(h.transform);
        if (playerFlag != null) playerFlag.DropIfCarriedBy(h.transform);

        if (suddenDeath) { EndMatch(Team.Player); return; }
        playerKills++;
        CheckGoal();
    }

    void OnCaptured(Team scoringTeam)
    {
        if (scoringTeam == Team.Player) playerCaptures++; else enemyCaptures++;
        CheckGoal();
    }

    void CheckGoal()
    {
        if (CurrentMode == Mode.Deathmatch)
        {
            if (playerKills >= deathmatchKillGoal) EndMatch(Team.Player);
            else if (enemyKills >= deathmatchKillGoal) EndMatch(Team.Enemy);
        }
        else if (CurrentMode == Mode.CaptureTheFlag)
        {
            if (playerCaptures >= ctfCaptureGoal) EndMatch(Team.Player);
            else if (enemyCaptures >= ctfCaptureGoal) EndMatch(Team.Enemy);
        }
    }

    // Timer hit zero with nobody at the goal: both modes fall back to whoever
    // has more eliminations, then to DeathZone sudden death if that's tied too.
    void ResolveAtBuzzer()
    {
        if (playerKills > enemyKills) { EndMatch(Team.Player); return; }
        if (enemyKills > playerKills) { EndMatch(Team.Enemy); return; }

        suddenDeath = true;
        deathZone.BeginShrinking();
        screenFx.ShowMessage("SUDDEN DEATH!");
        StartCoroutine(ClearBannerAfter(2.5f));
    }

    // Gridlock's buzzer rule is simpler than Deathmatch/CTF's: higher
    // percentage just wins outright, and an exact tie is a flat draw - this
    // mode never hands off to DeathZone.
    void ResolveGridlockAtBuzzer()
    {
        if (playerZonePercent > enemyZonePercent) EndMatch(Team.Player);
        else if (enemyZonePercent > playerZonePercent) EndMatch(Team.Enemy);
        else EndMatchDraw();
    }

    void EndMatch(Team winner)
    {
        matchRunning = false;
        suddenDeath = false;
        deathZone.Stop();
        Unsubscribe();
        // Otherwise a car that died on the very last hit could pop back to
        // life - and steal the countdown label - mid-cinematic.
        if (matchDirector != null) matchDirector.CancelPendingRespawns();

        screenFx.ShowMessage(winner == Team.Player ? "VICTORY!" : "DEFEAT!");
        StartCoroutine(PlayEndSequence());
    }

    void EndMatchDraw()
    {
        matchRunning = false;
        suddenDeath = false;
        deathZone.Stop();
        Unsubscribe();
        if (matchDirector != null) matchDirector.CancelPendingRespawns();

        screenFx.ShowMessage("DRAW!");
        StartCoroutine(PlayEndSequence());
    }

    IEnumerator ClearBannerAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        screenFx.HideCountdown();
    }

    // Locks controls (cars coast to a stop under their own decel curve, guns
    // fall silent) and pulls the camera back; once every car has actually
    // settled and the camera's done easing back (or postMatchMaxWait runs out
    // as a safety net), holds for one quiet beat, then reopens the menu.
    IEnumerator PlayEndSequence()
    {
        LockControls(true);
        if (cameraFollow != null) cameraFollow.ZoomOut(postMatchZoomDuration);

        float waited = 0f;
        while (waited < postMatchMaxWait)
        {
            bool cameraSettled = cameraFollow == null || !cameraFollow.IsZooming;
            if (cameraSettled && AllCarsSettled()) break;
            waited += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(postMatchMenuDelay);

        screenFx.HideCountdown();
        LockControls(false);
        if (cameraFollow != null) cameraFollow.ResetZoomImmediate();
        if (modeMenu != null) modeMenu.OpenMenu();
    }

    bool AllCarsSettled()
    {
        foreach (CarController car in allCars)
        {
            if (!CarSettled(car)) return false;
        }
        return true;
    }

    // A disabled CarController means Respawner already froze this car (dead or
    // mid drop-in) - nothing left to wait on either way.
    static bool CarSettled(CarController car)
    {
        if (car == null || !car.enabled) return true;
        return car.CurrentSpeed <= 0.05f;
    }

    void LockControls(bool locked)
    {
        foreach (CarController car in allCars) if (car != null) car.inputLocked = locked;
        foreach (Weapon w in allWeapons) if (w != null) w.inputLocked = locked;
        foreach (EnemyDriverAI ai in allAI) if (ai != null) ai.enabled = !locked;
    }

    // ---- HUD ----

    void UpdateHud()
    {
        string score;
        switch (CurrentMode)
        {
            case Mode.Deathmatch:
                score = $"YOU {playerKills} - {enemyKills} ENEMY   (first to {deathmatchKillGoal})";
                break;
            case Mode.CaptureTheFlag:
                score = $"YOU {playerCaptures} - {enemyCaptures} ENEMY   (first to {ctfCaptureGoal})   kills {playerKills}-{enemyKills}";
                break;
            case Mode.Gridlock:
                score = $"YOU {Mathf.FloorToInt(playerZonePercent)}% - {Mathf.FloorToInt(enemyZonePercent)}% ENEMY   (first to 100%)";
                break;
            default: // DeathZoneTest
                score = "DEATH ZONE TEST";
                break;
        }

        string status = suddenDeath ? "SUDDEN DEATH" : FormatTime(timeRemaining);

        string flagStatus = "";
        if (CurrentMode == Mode.CaptureTheFlag)
        {
            if (enemyFlag != null && enemyFlag.IsCarried) flagStatus = "\nYOU HAVE THE ENEMY FLAG!";
            else if (playerFlag != null && playerFlag.IsCarried) flagStatus = "\nENEMY HAS YOUR FLAG!";
        }

        screenFx.SetHud($"{score}\n{status}{flagStatus}");
    }

    static string FormatTime(float t)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0f, t));
        return $"{s / 60:00}:{s % 60:00}";
    }
}
