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

    // The car's original placement, cached in Awake. TestDummy pins to this.
    public Vector3 SpawnPosition => spawnPos;
    public Quaternion SpawnRotation => spawnRot;

    private Health health;
    private Rigidbody body;
    private Collider bodyCollider;
    private MeshRenderer[] meshes;
    private HealthBar bar;
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
        transform.SetPositionAndRotation(spawnPos + Vector3.up * dropHeight, spawnRot);
        health.Revive();

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
