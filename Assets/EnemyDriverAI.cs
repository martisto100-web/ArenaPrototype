using UnityEngine;

// ATTACH THIS TO: the EnemyCar root, alongside CarController + Weapon + Health.
// Feeds CarController and Weapon through IVehicleInput exactly like the player's
// joysticks do, so the enemy drives and shoots with identical stats.
//
// Behaviour shifts with the enemy's own remaining health:
//   healthy  (> aggressiveAbove) -> Aggressive: close to short range, keep firing
//   wounded  (> cautiousAbove)   -> Cautious:   hold a preferred gap, circle, fire in range
//   critical (<= cautiousAbove)  -> Desperate:  drive straight into the player, still firing
[RequireComponent(typeof(CarController))]
public class EnemyDriverAI : MonoBehaviour, IVehicleInput
{
    [Header("Target")]
    public Transform target;                       // the player; auto-found by tag if left empty
    public string targetTag = "Player";
    public float retargetInterval = 1f;            // retry lookup this often while target is missing

    [Header("Health thresholds (fraction of max health)")]
    [Range(0f, 1f)] public float aggressiveAbove = 0.66f;
    [Range(0f, 1f)] public float cautiousAbove = 0.33f;

    [Header("Engagement distances (metres)")]
    public float aggressiveRange = 6f;             // healthy: hug this distance
    public float preferredDistance = 14f;          // wounded: hold this gap
    public float distanceTolerance = 2.5f;         // dead zone so it doesn't jitter in and out
    public float attackRange = 24f;                // only fire when the target is closer than this

    public Vector2 MoveInput { get; private set; }
    public Vector2 AimInput { get; private set; }

    public enum Stance { Aggressive, Cautious, Desperate }
    public Stance CurrentStance { get; private set; }

    private Health health;
    private float retargetTimer;

    void Awake()
    {
        health = GetComponent<Health>();
        AcquireTarget();
    }

    void AcquireTarget()
    {
        if (target != null) return;
        GameObject go = GameObject.FindGameObjectWithTag(targetTag);
        if (go != null) target = go.transform;
    }

    void Update()
    {
        if (target == null)
        {
            retargetTimer -= Time.deltaTime;
            if (retargetTimer <= 0f)
            {
                AcquireTarget();
                retargetTimer = retargetInterval;
            }
            MoveInput = Vector2.zero;
            AimInput = Vector2.zero;
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        Vector3 dir = dist > 0.001f ? toTarget / dist : transform.forward;

        CurrentStance = EvaluateStance();

        float holdDistance;
        bool circleWhenInBand;
        switch (CurrentStance)
        {
            case Stance.Aggressive:
                holdDistance = aggressiveRange;
                circleWhenInBand = false;
                break;
            case Stance.Desperate:
                holdDistance = 0f;               // ram straight through
                circleWhenInBand = false;
                break;
            default: // Cautious
                holdDistance = preferredDistance;
                circleWhenInBand = true;
                break;
        }

        Vector3 move;
        if (dist > holdDistance + distanceTolerance)
            move = dir;                                       // advance
        else if (dist < holdDistance - distanceTolerance)
            move = -dir;                                      // back off
        else if (circleWhenInBand)
            move = Vector3.Cross(Vector3.up, dir);            // strafe / circle
        else
            move = Vector3.zero;                              // hold and shoot

        MoveInput = new Vector2(move.x, move.z);
        AimInput = dist <= attackRange ? new Vector2(dir.x, dir.z) : Vector2.zero;
    }

    Stance EvaluateStance()
    {
        float frac = health != null ? health.HealthFraction : 1f;
        if (frac > aggressiveAbove) return Stance.Aggressive;
        if (frac > cautiousAbove) return Stance.Cautious;
        return Stance.Desperate;
    }
}
