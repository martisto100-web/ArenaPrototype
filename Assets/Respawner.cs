using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ATTACH THIS TO: a car root (with Health + Rigidbody + a Collider).
// Instead of being destroyed on death, the car is hidden and frozen. When
// MatchDirector calls Respawn(), it is placed high above its start position and
// dropped back in by gravity; controls / AI stay off until it has landed.
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody))]
public class Respawner : MonoBehaviour
{
    [Header("Drop-in respawn")]
    public float dropHeight = 7f;
    public float dropSpeed = 12f;         // initial downward speed so the drop is quick
    public float landClearance = 0.7f;    // car pivot within this of a surface = landed, controls return
    public float maxFallTime = 2.5f;      // safety cap if it somehow never lands

    public bool IsDead { get; private set; }

    private Vector3 spawnPos;
    private Quaternion spawnRot;
    private Vector3? overridePos;    // set by KnockoutManager for its bigger-arena bases
    private Quaternion? overrideRot;

    // The car's original placement, cached in Awake (or the current override,
    // if one's been set) - TestDummy pins to this, and it's where Respawn()
    // drops the car back in.
    public Vector3 SpawnPosition => overridePos ?? spawnPos;
    public Quaternion SpawnRotation => overrideRot ?? spawnRot;

    // Knockout uses this to drop cars at team bases in its own, bigger arena
    // instead of this car's normal scene-authored spot. ClearSpawnOverride()
    // restores normal behaviour for every other mode.
    public void SetSpawnOverride(Vector3 pos, Quaternion rot)
    {
        overridePos = pos;
        overrideRot = rot;
    }

    public void ClearSpawnOverride()
    {
        overridePos = null;
        overrideRot = null;
    }

    private Health health;
    private Rigidbody body;
    private Collider bodyCollider;
    private MeshRenderer[] meshes;
    private HealthBar bar;
    private CarController carController;
    private MonoBehaviour[] controlScripts; // CarController / Weapon / EnemyDriverAI / PlayerInputRouter
    private int groundMask;

    void Awake()
    {
        spawnPos = transform.position;
        spawnRot = transform.rotation;

        health = GetComponent<Health>();
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        meshes = GetComponentsInChildren<MeshRenderer>();
        bar = GetComponent<HealthBar>();
        carController = GetComponent<CarController>();
        controlScripts = CollectControls();
        groundMask = ~(1 << gameObject.layer); // everything except this car's own layer

        health.destroyOnDeath = false;
        health.Died += HandleDeath;
    }

    void OnDestroy()
    {
        if (health != null) health.Died -= HandleDeath;
    }

    MonoBehaviour[] CollectControls()
    {
        List<MonoBehaviour> list = new List<MonoBehaviour>();
        void Add(MonoBehaviour m) { if (m != null) list.Add(m); }

        Add(GetComponent<CarController>());
        Add(GetComponent<Weapon>());
        Add(GetComponent<EnemyDriverAI>());
        Add(GetComponent<PlayerInputRouter>());
        Add(GetComponent<EngineAudio>()); // OnDisable silences the engine loops while dead
        return list.ToArray();
    }

    void HandleDeath(Health h)
    {
        IsDead = true;
        SetControls(false);

        if (bodyCollider != null) bodyCollider.enabled = false;
        foreach (MeshRenderer m in meshes) if (m != null) m.enabled = false;
        if (bar != null) bar.Hidden = true;

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }

    public void Respawn()
    {
        StopAllCoroutines();
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        transform.SetPositionAndRotation(SpawnPosition + Vector3.up * dropHeight, SpawnRotation);
        health.Revive();
        // Otherwise a car that died mid-acceleration keeps that speed frozen in
        // CarController while disabled, then lurches forward under it the
        // instant controls return - even with input locked, since it's not
        // input driving the motion, it's this stale leftover value.
        if (carController != null) carController.ResetSpeed();

        if (bodyCollider != null) bodyCollider.enabled = true;
        foreach (MeshRenderer m in meshes) if (m != null) m.enabled = true;
        if (bar != null) bar.Hidden = false;

        if (body != null)
        {
            body.isKinematic = false;
            body.linearVelocity = Vector3.down * dropSpeed;
            body.angularVelocity = Vector3.zero;
        }

        // hand control back the instant it touches down (timeout is just a safety net)
        float t = 0f;
        while (t < maxFallTime && !IsGrounded())
        {
            t += Time.deltaTime;
            yield return null;
        }

        SetControls(true);
        IsDead = false;
    }

    bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
        {
            return (transform.position.y - hit.point.y) <= landClearance;
        }
        return false;
    }

    void SetControls(bool on)
    {
        foreach (MonoBehaviour m in controlScripts)
        {
            if (m != null) m.enabled = on;
        }
    }
}
