using UnityEngine;

// ATTACH THIS TO: an enemy car to turn it into an inert practice target.
// Added at runtime by ModeMenu when "Test Arena" is picked.
// Kills the AI / driving / weapon / engine (re-killing them every frame so a
// Respawner-driven respawn can't switch them back on) and pins the body at its
// spawn spot so nothing shoves it. Health, Respawner, DamageFx and HealthBar are
// left alone, so it still takes damage, blows up with the same FX as the AI
// enemy, and runs the "Enemy respawns in" countdown before dropping back in.
public class TestDummy : MonoBehaviour
{
    private Respawner respawner;
    private Rigidbody body;
    private EnemyDriverAI ai;
    private CarController car;
    private Weapon weapon;
    private EngineAudio engine;

    private Vector3 homePos;
    private Quaternion homeRot;

    void Awake()
    {
        respawner = GetComponent<Respawner>();
        body = GetComponent<Rigidbody>();
        ai = GetComponent<EnemyDriverAI>();
        car = GetComponent<CarController>();
        weapon = GetComponent<Weapon>();
        engine = GetComponent<EngineAudio>();

        // Pin to the arena's designated enemy spot, not wherever the AI happened
        // to be when Test Arena was picked mid-game.
        if (respawner != null)
        {
            homePos = respawner.SpawnPosition;
            homeRot = respawner.SpawnRotation;
        }
        else
        {
            homePos = transform.position;
            homeRot = transform.rotation;
        }

        Silence();
    }

    void LateUpdate()
    {
        // Respawner.RespawnRoutine flips these back on; keep them off.
        Silence();

        // While fully alive (not mid death / drop-in), hold it exactly in place.
        bool droppingInOrDead = respawner != null && respawner.IsDead;
        if (!droppingInOrDead)
        {
            transform.SetPositionAndRotation(homePos, homeRot);
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }

    void Silence()
    {
        if (ai != null && ai.enabled) ai.enabled = false;
        if (car != null && car.enabled) car.enabled = false;
        if (weapon != null && weapon.enabled) weapon.enabled = false;
        if (engine != null && engine.enabled) engine.enabled = false;
    }

    // Called by ModeMenu when switching back to 1v1: hand the car back its
    // controls and remove this component.
    public void Detach()
    {
        enabled = false;
        if (ai != null) ai.enabled = true;
        if (car != null) car.enabled = true;
        if (weapon != null) weapon.enabled = true;
        if (engine != null) engine.enabled = true;
        Destroy(this);
    }
}
