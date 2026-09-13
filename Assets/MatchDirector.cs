using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ATTACH THIS TO: an empty "GameDirector" object (with a ScreenFx alongside).
// Watches every car that has a Respawner. On a death it plays the kill juice
// (camera shake + screen flash), runs the on-screen respawn countdown, then
// drops the car back in. CancelPendingRespawns() lets MatchModeManager cut
// this short the instant a match ends, so a losing car can't pop back to life
// (and hijack the shared countdown label) mid-victory-cinematic.
// RespawnsSuspended goes further: while true, a death never starts a respawn
// countdown at all (though the shake/flash juice still plays) - Knockout sets
// this for the whole series, since a car eliminated mid-round should just stay
// down until KnockoutManager resets everyone for the next round.
[RequireComponent(typeof(ScreenFx))]
public class MatchDirector : MonoBehaviour
{
    [Header("Respawn")]
    public int respawnSeconds = 5;

    public bool RespawnsSuspended { get; set; }

    [Header("Kill juice")]
    public float shakeDuration = 0.5f;
    public float shakeMagnitude = 0.9f;
    public float landingShakeDuration = 0.22f;
    public float landingShakeMagnitude = 0.4f;

    private ScreenFx screenFx;
    private CameraFollow cameraFollow;
    private readonly List<Health> tracked = new List<Health>();
    private bool playerRespawning;

    void Start()
    {
        screenFx = GetComponent<ScreenFx>();
        if (Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();

        foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
        {
            if (h.GetComponent<Respawner>() == null) continue;
            h.Died += OnDied;
            tracked.Add(h);
        }
    }

    void OnDestroy()
    {
        foreach (Health h in tracked)
        {
            if (h != null) h.Died -= OnDied;
        }
    }

    // Stops any in-flight "Respawning in: N" countdown(s) dead and clears the
    // shared-label flag, without touching the car itself - it stays exactly as
    // dead/frozen as Respawner already left it. Safe to call even when nothing
    // is pending.
    public void CancelPendingRespawns()
    {
        StopAllCoroutines();
        playerRespawning = false;
    }

    void OnDied(Health h)
    {
        Respawner respawner = h.GetComponent<Respawner>();
        TeamMember tm = h.GetComponent<TeamMember>();
        bool isPlayer = tm == null || tm.team == Team.Player;

        if (cameraFollow != null && GameSettings.ScreenShakeOnElimination) cameraFollow.Shake(shakeDuration, shakeMagnitude);
        if (screenFx != null && GameSettings.ScreenFlashOnElimination) screenFx.Flash();

        if (RespawnsSuspended) return; // Knockout - this car is out until the round resets

        string label = isPlayer ? "Respawning in:" : "Enemy respawns in:";
        StartCoroutine(RespawnRoutine(respawner, label, isPlayer));
    }

    IEnumerator RespawnRoutine(Respawner respawner, string label, bool isPlayer)
    {
        if (isPlayer) playerRespawning = true;

        int secs = Mathf.Max(1, respawnSeconds);
        for (int n = secs; n >= 1; n--)
        {
            // the player's own countdown always wins the shared label
            if (isPlayer || !playerRespawning) screenFx.ShowCountdown(label, n);
            yield return new WaitForSeconds(1f);
        }

        if (isPlayer)
        {
            playerRespawning = false;
            screenFx.HideCountdown();
        }
        else if (!playerRespawning)
        {
            screenFx.HideCountdown();
        }

        if (respawner != null) respawner.Respawn();
        if (cameraFollow != null && GameSettings.ScreenShakeOnElimination) cameraFollow.Shake(landingShakeDuration, landingShakeMagnitude);
    }
}
