using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ATTACH THIS TO: an empty "GameDirector" object (with a ScreenFx alongside).
// Watches every car that has a Respawner. On a death it plays the kill juice
// (camera shake + screen flash), runs the on-screen respawn countdown, then
// drops the car back in.
[RequireComponent(typeof(ScreenFx))]
public class MatchDirector : MonoBehaviour
{
    [Header("Respawn")]
    public int respawnSeconds = 5;

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

    void OnDied(Health h)
    {
        Respawner respawner = h.GetComponent<Respawner>();
        TeamMember tm = h.GetComponent<TeamMember>();
        bool isPlayer = tm == null || tm.team == Team.Player;

        if (cameraFollow != null) cameraFollow.Shake(shakeDuration, shakeMagnitude);
        if (screenFx != null) screenFx.Flash();

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
        if (cameraFollow != null) cameraFollow.Shake(landingShakeDuration, landingShakeMagnitude);
    }
}
