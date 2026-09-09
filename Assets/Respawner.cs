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
    public float dropSpeed = 12f;           // initial downward speed so the drop is quick
    public float controlReturnDelay = 0.85f; // physics-only fall time before controls/AI resume

    public bool IsDead { get; private set; }

    private Vector3 spawnPos;
    private Quaternion spawnRot;

    private Health health;
    private Rigidbody body;
    private Collider bodyCollider;
    private MeshRenderer[] meshes;
    private HealthBar bar;
    private MonoBehaviour[] controlScripts; // CarController / Weapon / EnemyDriverAI / PlayerInputRouter

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

        yield return new WaitForSeconds(controlReturnDelay); // let it fall and settle

        SetControls(true);
        IsDead = false;
    }

    void SetControls(bool on)
    {
        foreach (MonoBehaviour m in controlScripts)
        {
            if (m != null) m.enabled = on;
        }
    }
}
