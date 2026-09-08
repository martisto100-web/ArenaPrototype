using System.Collections.Generic;
using UnityEngine;

// ATTACH THIS TO: the EnemyCar root, alongside CarController + Weapon + Health.
// Feeds CarController/Weapon through IVehicleInput, so the enemy drives and
// shoots with the exact same stats as the player.
//
// Playstyle: it never charges the player. It holds a stand-off distance and
// circles, flipping orbit direction at random intervals, drifting with a bit
// of wander, sidestepping the player's bullets, and steering around obstacles.
// The more hurt it is, the wider it orbits and the twitchier it dodges.
[RequireComponent(typeof(CarController))]
public class EnemyDriverAI : MonoBehaviour, IVehicleInput
{
    [Header("Target")]
    public Transform target;                       // the player; auto-found by tag if left empty
    public string targetTag = "Player";
    public float retargetInterval = 1f;

    [Header("Stand-off distance (metres)")]
    public float pressDistance = 10f;              // healthy: circle this far out
    public float evadeDistance = 17f;              // hurt: circle further out
    [Range(0f, 1f)] public float evadeBelowHealth = 0.5f;
    public float rangeSoftness = 5f;               // larger = gentler correction back to the orbit radius

    [Header("Circling")]
    public float orbitFlipMin = 1.5f;             // seconds between orbit-direction flips (min / max)
    public float orbitFlipMax = 4f;
    [Range(0f, 1f)] public float wanderWeight = 0.2f;
    public float wanderFrequency = 0.35f;

    [Header("Bullet dodging")]
    public LayerMask projectileMask = 1 << 6;      // the "Projectiles" layer
    public float dodgeScanRadius = 12f;            // how far ahead it can notice a bullet
    public float dodgeCorridor = 3f;               // only dodge bullets whose path passes closer than this
    [Range(0f, 1f)] public float dodgeChance = 0.6f; // per bullet: does it even attempt a dodge
    public float reactionDelay = 0.22f;            // seconds a noticed bullet is ignored before it reacts
    public float dodgeWeight = 0.9f;

    [Header("Obstacle avoidance")]
    public float feelerLength = 5f;
    public float avoidWeight = 1.6f;

    [Header("Firing")]
    public float attackRange = 24f;

    public Vector2 MoveInput { get; private set; }
    public Vector2 AimInput { get; private set; }

    public enum Stance { Pressing, Evasive }
    public Stance CurrentStance { get; private set; }

    private Health health;
    private Collider selfCollider;
    private Team myTeam = Team.Enemy;
    private float retargetTimer;
    private int orbitDir = 1;
    private float orbitFlipTimer;
    private float wanderSeed;

    // Per-bullet memory so the dodge has human-like lag: when we first saw a
    // bullet, and whether we rolled to bother dodging it at all.
    private struct Threat { public float firstSeen; public bool committed; }
    private readonly Dictionary<int, Threat> threats = new Dictionary<int, Threat>();
    private readonly HashSet<int> liveThreats = new HashSet<int>();
    private readonly List<int> staleThreats = new List<int>();

    void Awake()
    {
        health = GetComponent<Health>();
        selfCollider = GetComponent<Collider>();

        TeamMember member = GetComponent<TeamMember>();
        if (member != null) myTeam = member.team;

        wanderSeed = Random.value * 100f;
        orbitDir = Random.value < 0.5f ? -1 : 1;
        ScheduleOrbitFlip();
        AcquireTarget();
    }

    void AcquireTarget()
    {
        if (target != null) return;
        GameObject go = GameObject.FindGameObjectWithTag(targetTag);
        if (go != null) target = go.transform;
    }

    void ScheduleOrbitFlip()
    {
        orbitFlipTimer = Random.Range(orbitFlipMin, orbitFlipMax);
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

        Vector3 selfPos = transform.position;
        Vector3 toTarget = target.position - selfPos;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        Vector3 dir = dist > 0.001f ? toTarget / dist : transform.forward;

        float frac = health != null ? health.HealthFraction : 1f;
        CurrentStance = frac <= evadeBelowHealth ? Stance.Evasive : Stance.Pressing;
        float orbitDistance = CurrentStance == Stance.Evasive ? evadeDistance : pressDistance;
        float circleGain = CurrentStance == Stance.Evasive ? 1.15f : 0.9f;

        orbitFlipTimer -= Time.deltaTime;
        if (orbitFlipTimer <= 0f)
        {
            orbitDir = -orbitDir;
            ScheduleOrbitFlip();
        }

        // radial: pull back toward the orbit radius (+dir = toward player, -dir = away)
        float radialErr = dist - orbitDistance;
        Vector3 radial = dir * Mathf.Clamp(radialErr / rangeSoftness, -1f, 1f);

        // tangential: circle the player, direction flips over time
        Vector3 tangent = Vector3.Cross(Vector3.up, dir) * (orbitDir * circleGain);

        // wander: slow Perlin drift so the path isn't a clean circle
        float wanderAngle = (Mathf.PerlinNoise(Time.time * wanderFrequency, wanderSeed) - 0.5f) * 2f * Mathf.PI;
        Vector3 wander = new Vector3(Mathf.Cos(wanderAngle), 0f, Mathf.Sin(wanderAngle)) * wanderWeight;

        Vector3 dodge = ComputeDodge(selfPos) * dodgeWeight;
        Vector3 avoid = ComputeAvoidance(selfPos) * avoidWeight;

        Vector3 move = radial + tangent + wander + dodge + avoid;
        move = Vector3.ClampMagnitude(move, 1f);
        MoveInput = new Vector2(move.x, move.z);

        AimInput = dist <= attackRange ? new Vector2(dir.x, dir.z) : Vector2.zero;
    }

    // Sum of sideways pushes away from the paths of incoming player bullets.
    // A bullet is only acted on if we rolled to dodge it (dodgeChance) AND it has
    // been in view for at least reactionDelay seconds, so plenty of shots land.
    Vector3 ComputeDodge(Vector3 selfPos)
    {
        Collider[] hits = Physics.OverlapSphere(selfPos, dodgeScanRadius, projectileMask, QueryTriggerInteraction.Collide);
        Vector3 push = Vector3.zero;
        liveThreats.Clear();

        foreach (Collider c in hits)
        {
            Projectile p = c.GetComponentInParent<Projectile>();
            if (p == null || p.team == myTeam) continue;

            int id = p.GetInstanceID();
            liveThreats.Add(id);

            if (!threats.TryGetValue(id, out Threat t))
            {
                t = new Threat { firstSeen = Time.time, committed = Random.value < dodgeChance };
                threats[id] = t;
            }
            if (!t.committed || Time.time - t.firstSeen < reactionDelay) continue;

            Vector3 bulletPos = c.transform.position;
            Vector3 bulletFwd = c.transform.forward;
            bulletFwd.y = 0f;
            if (bulletFwd.sqrMagnitude < 0.001f) continue;
            bulletFwd.Normalize();

            Vector3 toSelf = selfPos - bulletPos;
            toSelf.y = 0f;
            float along = Vector3.Dot(toSelf, bulletFwd);
            if (along <= 0f || along > dodgeScanRadius) continue; // already past us, or too far

            Vector3 perp = toSelf - bulletFwd * along; // our offset from the bullet's line
            float perpDist = perp.magnitude;
            if (perpDist > dodgeCorridor) continue;    // it will miss anyway

            Vector3 side = perpDist > 0.05f ? perp / perpDist : Vector3.Cross(Vector3.up, bulletFwd);
            float urgency = 1f - along / dodgeScanRadius;
            push += side * (0.5f + urgency);
        }

        // Forget bullets we can no longer see, so the dictionary stays tiny.
        if (threats.Count > 0)
        {
            staleThreats.Clear();
            foreach (KeyValuePair<int, Threat> kv in threats)
            {
                if (!liveThreats.Contains(kv.Key)) staleThreats.Add(kv.Key);
            }
            for (int i = 0; i < staleThreats.Count; i++) threats.Remove(staleThreats[i]);
        }

        return push;
    }

    // Three forward feelers; if one hits static geometry, push away from its surface.
    Vector3 ComputeAvoidance(Vector3 selfPos)
    {
        Vector3 origin = selfPos + Vector3.up * 0.3f;
        Vector3 result = Vector3.zero;

        for (int i = 0; i < 3; i++)
        {
            float angle = i == 0 ? 0f : (i == 1 ? 25f : -25f);
            Vector3 feeler = Quaternion.Euler(0f, angle, 0f) * transform.forward;

            if (!Physics.Raycast(origin, feeler, out RaycastHit hit, feelerLength, ~0, QueryTriggerInteraction.Ignore))
                continue;
            if (hit.collider == selfCollider) continue;
            if (hit.collider.GetComponentInParent<TeamMember>() != null) continue; // ignore vehicles
            if (hit.collider.GetComponentInParent<Projectile>() != null) continue; // ignore bullets

            Vector3 away = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
            if (away.sqrMagnitude < 0.001f) continue;
            result += away.normalized * (1f - hit.distance / feelerLength);
        }

        return result;
    }
}
