using System.Collections;
using UnityEngine;

// ATTACH THIS TO: the GameDirector object (alongside ModeMenu / ScreenFx).
// Runs the two structured modes ModeMenu can start:
//   Deathmatch        - first team to killGoal eliminations wins; 3 min timer.
//   Capture the Flag  - first team to captureGoal flag captures wins; 4 min timer.
// In both, if nobody hits the goal before the timer runs out the team with more
// eliminations wins (captures are CTF's win condition, kills are just its
// tiebreaker); if THAT'S also tied, DeathZone sudden death decides it - first
// car to go down loses. Kills/respawns themselves are still all MatchDirector /
// Respawner as always; this only watches the score and owns the HUD text
// (drawn through ScreenFx) and Flag setup for CTF.
[RequireComponent(typeof(ScreenFx))]
[RequireComponent(typeof(DeathZone))]
public class MatchModeManager : MonoBehaviour
{
    public enum Mode { None, Deathmatch, CaptureTheFlag }

    [Header("Deathmatch")]
    public int deathmatchKillGoal = 10;
    public float deathmatchDuration = 300f;

    [Header("Capture The Flag")]
    public int ctfCaptureGoal = 3;
    public float ctfDuration = 240f;

    [Header("Post-match")]
    public float winBannerSeconds = 4f;

    public Mode CurrentMode { get; private set; } = Mode.None;

    private ScreenFx screenFx;
    private DeathZone deathZone;
    private ModeMenu modeMenu;

    private Health playerHealth;
    private Health enemyHealth;
    private Flag playerFlag; // the Player team's own flag/base
    private Flag enemyFlag;  // the Enemy team's own flag/base

    private int playerKills;
    private int enemyKills;
    private int playerCaptures;
    private int enemyCaptures;
    private float timeRemaining;
    private bool matchRunning;
    private bool suddenDeath;

    void Awake()
    {
        screenFx = GetComponent<ScreenFx>();
        deathZone = GetComponent<DeathZone>();
        modeMenu = GetComponent<ModeMenu>();
    }

    void OnDestroy() => Unsubscribe();

    void FindCombatants()
    {
        playerHealth = null;
        enemyHealth = null;
        foreach (TeamMember tm in FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            Health h = tm.GetComponent<Health>();
            if (h == null) continue;
            if (tm.team == Team.Player) playerHealth = h; else enemyHealth = h;
        }

        playerFlag = null;
        enemyFlag = null;
        foreach (Flag f in FindObjectsByType<Flag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (f.owningTeam == Team.Player) playerFlag = f; else enemyFlag = f;
        }
        if (playerFlag != null) playerFlag.SetOther(enemyFlag);
        if (enemyFlag != null) enemyFlag.SetOther(playerFlag);
    }

    // ---- called by ModeMenu on every mode pick (including switching to 1v1 / Test / None) ----

    public void StartMatch(Mode mode)
    {
        StopAllCoroutines();
        Unsubscribe();
        FindCombatants();

        CurrentMode = mode;
        matchRunning = mode != Mode.None;
        suddenDeath = false;
        playerKills = enemyKills = playerCaptures = enemyCaptures = 0;
        deathZone.Stop();

        SetFlagsActive(mode == Mode.CaptureTheFlag);

        if (!matchRunning)
        {
            screenFx.HideHud();
            return;
        }

        timeRemaining = mode == Mode.Deathmatch ? deathmatchDuration : ctfDuration;

        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        if (enemyHealth != null) enemyHealth.Died += OnEnemyDied;
        if (mode == Mode.CaptureTheFlag)
        {
            if (playerFlag != null) playerFlag.Captured += OnCaptured;
            if (enemyFlag != null) enemyFlag.Captured += OnCaptured;
        }

        UpdateHud();
    }

    void SetFlagsActive(bool on)
    {
        if (playerFlag != null) playerFlag.gameObject.SetActive(on);
        if (enemyFlag != null) enemyFlag.gameObject.SetActive(on);
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

        if (!suddenDeath)
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                ResolveAtBuzzer();
            }
        }
        UpdateHud();
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

    void EndMatch(Team winner)
    {
        matchRunning = false;
        suddenDeath = false;
        deathZone.Stop();
        Unsubscribe();

        screenFx.ShowMessage(winner == Team.Player ? "YOU WIN!" : "ENEMY WINS!");
        StartCoroutine(ReturnToMenuAfter(winBannerSeconds));
    }

    IEnumerator ClearBannerAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        screenFx.HideCountdown();
    }

    IEnumerator ReturnToMenuAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        screenFx.HideCountdown();
        if (modeMenu != null) modeMenu.OpenMenu();
    }

    // ---- HUD ----

    void UpdateHud()
    {
        string score = CurrentMode == Mode.Deathmatch
            ? $"YOU {playerKills} - {enemyKills} ENEMY   (first to {deathmatchKillGoal})"
            : $"YOU {playerCaptures} - {enemyCaptures} ENEMY   (first to {ctfCaptureGoal})   kills {playerKills}-{enemyKills}";

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
