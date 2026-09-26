using UnityEngine;

// ATTACH THIS TO: your rocket prefab (not to the vehicle).
// Fired by Weapon when its weaponType is RocketLauncher. Flies forward like
// Projectile, but when launched with a target (Weapon picks one, or not, at
// the instant it fires - see Weapon.FindRocketTarget) it steers toward that
// car at a limited turn rate, never an instant snap, so a target that cuts a
// sharp angle - or gets an obstacle between itself and the rocket - can
// genuinely dodge it. Launched with no target, it just flies straight.
//
// Tracking from here on is entirely the rocket's own, independent of the
// Weapon that fired it (which may have long since re-aimed elsewhere): every
// frame it re-checks the target is still within trackingRange/
// trackingHalfAngle of its OWN current heading and still in line of sight.
// The moment any of those fails the lock drops for good - it continues
// straight from whatever direction it was last heading, and never
// re-acquires.
//
// No splash damage - a hit only damages whatever collider it actually
// touched (see OnTriggerEnter), same as Projectile. Self-destructs (no
// damage, a small harmless puff) after lifeTime if it never connects with
// anything - walls, cars, or (eventually) arena decor - so it can't home or
// fly across the map forever. team/target are set by Weapon right after
// Instantiate.
public class Rocket : MonoBehaviour
{
    [Header("Flight")]
    public float speed = 18f;   // slower than the 40-speed bullet - a car (top speed 12) can outrun/dodge it
    public float damage = 15f;
    public float lifeTime = 4f; // self-destruct if it never hits anything

    [Header("Homing (only while a target is set)")]
    public float turnRateDegPerSec = 120f; // limited on purpose - a sharp juke at close range can lose it
    public float trackingRange = 40f;
    public float trackingHalfAngle = 60f;

    // Spawning right at the shooter's firePoint means the rocket's own hitbox
    // starts out overlapping (or right on the boundary of) the shooter's own
    // car collider - especially firing back across your own car. The
    // same-team check in OnTriggerEnter already ignores that, but this makes
    // a self-hit structurally impossible instead of merely filtered: the
    // hitbox is off for this long after spawn, so it can't detonate on
    // anything (including a stray wall/car clipped right at launch) before
    // it's had a moment to clear.
    public float armDelay = 0.2f; // >= time to clear the car's own longest axis at `speed`, worst case (dead backward shot)

    [HideInInspector] public Team team = Team.Player;
    [HideInInspector] public Health target; // null = flies straight, never acquires

    private int obstacleMask;
    private bool detonated;
    private Collider hitbox;
    private float spawnTime;

    private static Material sharedPuffMat;
    private static Texture2D softDot;

    void Awake()
    {
        obstacleMask = LayerMask.GetMask("Default");
        hitbox = GetComponent<Collider>();
    }

    void Start()
    {
        spawnTime = Time.time;
        if (hitbox != null)
        {
            hitbox.enabled = false;
            Invoke(nameof(ArmHitbox), armDelay);
        }
        Invoke(nameof(SelfDestruct), lifeTime);
    }

    void ArmHitbox()
    {
        if (hitbox != null) hitbox.enabled = true;
    }

    void Update()
    {
        if (target != null)
        {
            if (StillTracking())
            {
                Vector3 aimPoint = target.transform.position + Vector3.up * 0.5f;
                Vector3 desiredDir = aimPoint - transform.position;
                desiredDir.y = 0f;
                if (desiredDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion desiredRot = Quaternion.LookRotation(desiredDir, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRot, turnRateDegPerSec * Time.deltaTime);
                }
            }
            else
            {
                target = null; // lock lost for good - keep flying straight from here
            }
        }

        transform.position += transform.forward * speed * Time.deltaTime;
    }

    bool StillTracking()
    {
        if (target.IsDead) return false;

        Respawner rs = target.GetComponent<Respawner>();
        if (rs != null && rs.IsDead) return false;

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > trackingRange) return false;

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (Vector3.Angle(fwd, toTarget) > trackingHalfAngle) return false;

        if (Physics.Linecast(transform.position, target.transform.position, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return false; // something solid between the rocket and the target
        }
        return true;
    }

    void SelfDestruct()
    {
        Detonate("lifetime expired (" + lifeTime + "s, never connected)");
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Rocket>() != null || other.GetComponentInParent<Projectile>() != null)
        {
            return; // rockets/bullets don't collide with each other
        }

        TeamMember hitTeam = other.GetComponentInParent<TeamMember>();
        if (hitTeam != null && hitTeam.team == team)
        {
            Debug.Log($"[Rocket] ignored same-team overlap with '{other.name}' (team {hitTeam.team}) at t={Time.time - spawnTime:F2}s", this);
            return; // friendly fire / self hit — fly straight through
        }

        Health hitHealth = other.GetComponentInParent<Health>();
        if (hitHealth != null)
        {
            hitHealth.TakeDamage(damage); // direct hit only - no splash/radius damage
        }
        Detonate("hit '" + other.name + "'" + (hitHealth != null ? " (damaged)" : " (no Health - wall/obstacle)"));
    }

    // TEMP DIAGNOSTIC: logs exactly why/when each rocket ends, so a report of
    // "it just disappears" turns into a concrete cause in the Console instead
    // of another guess. Remove once the launch behaviour is confirmed solid.
    void Detonate(string reason)
    {
        if (detonated) return;
        detonated = true;
        CancelInvoke(nameof(SelfDestruct));
        Debug.Log($"[Rocket] detonated at t={Time.time - spawnTime:F2}s, pos={transform.position} - {reason}", this);
        SpawnPuff(transform.position);
        Destroy(gameObject);
    }

    // A small, harmless burst — used for both a hit and a mid-air self-destruct,
    // so a rocket never just silently vanishes.
    static void SpawnPuff(Vector3 pos)
    {
        GameObject go = new GameObject("RocketPuff");
        go.transform.position = pos;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startColor = new Color(1f, 0.55f, 0.15f, 1f);
        main.maxParticles = 24;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.25f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0f), new GradientColorKey(new Color(0.3f, 0.3f, 0.3f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.material = SharedPuffMaterial();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        Destroy(go, 1f);
    }

    static Material SharedPuffMaterial()
    {
        if (sharedPuffMat != null) return sharedPuffMat;
        sharedPuffMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftDot() };
        return sharedPuffMat;
    }

    static Texture2D SoftDot()
    {
        if (softDot != null) return softDot;
        const int size = 32;
        softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        softDot.Apply();
        return softDot;
    }
}
