using System.Collections;
using UnityEngine;

// ATTACH THIS TO: a car root with a Health component.
// Health-driven particle FX, all generated in code (no art assets):
//   HealthFraction < smokeBelow -> smoke plume
//   HealthFraction < fireBelow  -> flames
//   on death                    -> explosion burst + an expanding shockwave dome
// The FX rig is a separate object that follows the car, so the car's
// non-uniform scale doesn't distort the particles.
public class DamageFx : MonoBehaviour
{
    [Header("Thresholds (fraction of max health)")]
    [Range(0f, 1f)] public float smokeBelow = 0.5f;
    [Range(0f, 1f)] public float fireBelow = 0.3f;

    [Header("Placement")]
    public Vector3 worldOffset = new Vector3(0f, 0.4f, 0f);

    [Header("Shockwave")]
    public float shockwaveRadius = 6f;
    public float shockwaveDuration = 0.4f;
    public Color shockwaveColor = new Color(0.75f, 0.75f, 0.8f, 0.35f);

    private Health health;
    private Respawner respawner;
    private Transform rig;
    private ParticleSystem smoke;
    private ParticleSystem[] fires;
    private ParticleSystem explosion;
    private Transform shockwave;
    private Material shockwaveMat;
    private Coroutine shockwaveCo;

    private static Material sharedMat;
    private static Texture2D softDot;

    void Awake()
    {
        health = GetComponentInParent<Health>();
        respawner = GetComponentInParent<Respawner>();

        rig = new GameObject(name + "_DamageFx").transform;
        smoke = MakeSmoke();
        fires = new[]
        {
            MakeFire(new Vector3(0f, 0.05f, -0.1f), 0.75f, 40f),   // centre (now smaller)
            MakeFire(new Vector3(-0.6f, -0.05f, 0.35f), 0.5f, 26f), // left
            MakeFire(new Vector3(0.6f, -0.05f, 0.35f), 0.5f, 26f),  // right
            MakeFire(new Vector3(0f, -0.05f, -0.95f), 0.55f, 28f),  // rear
        };
        explosion = MakeExplosion();
        BuildShockwave();

        SetEmission(smoke, false);
        SetFires(false);

        if (health != null) health.Died += OnDied;
    }

    void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
        if (rig != null) Destroy(rig.gameObject);
    }

    void LateUpdate()
    {
        if (rig == null) return;
        rig.position = transform.position + worldOffset;
        rig.rotation = transform.rotation; // so the side/rear flames stay car-relative
    }

    void Update()
    {
        if (health == null) return;
        bool dead = health.IsDead || (respawner != null && respawner.IsDead);
        float f = health.HealthFraction;
        SetEmission(smoke, !dead && f < smokeBelow);
        SetFires(!dead && f < fireBelow);
    }

    void OnDied(Health h)
    {
        SetEmission(smoke, false);
        SetFires(false);
        if (explosion != null) { explosion.Clear(); explosion.Play(); }
        if (shockwave != null)
        {
            if (shockwaveCo != null) StopCoroutine(shockwaveCo);
            shockwaveCo = StartCoroutine(ShockwaveRoutine());
        }
    }

    IEnumerator ShockwaveRoutine()
    {
        shockwave.gameObject.SetActive(true);
        float t = 0f;
        while (t < shockwaveDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / shockwaveDuration);
            float eased = 1f - (1f - k) * (1f - k); // ease-out
            float d = Mathf.Lerp(0.6f, shockwaveRadius * 2f, eased);
            shockwave.localScale = new Vector3(d, d, d);
            if (shockwaveMat != null)
            {
                Color c = shockwaveColor;
                c.a = shockwaveColor.a * (1f - k);
                shockwaveMat.color = c;
            }
            yield return null;
        }
        shockwave.gameObject.SetActive(false);
    }

    void BuildShockwave()
    {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "shockwave";
        Collider col = s.GetComponent<Collider>();
        if (col != null) Destroy(col);
        s.transform.SetParent(rig, false);
        s.transform.localPosition = Vector3.zero;
        s.transform.localScale = Vector3.zero;

        MeshRenderer r = s.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        shockwaveMat = new Material(Shader.Find("Sprites/Default")) { color = shockwaveColor };
        r.material = shockwaveMat;

        shockwave = s.transform;
        s.SetActive(false);
    }

    // ---- builders ----

    ParticleSystem NewSystem(string systemName, bool loop)
    {
        GameObject go = new GameObject(systemName);
        go.transform.SetParent(rig, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        // One-shot systems must be stopped before setting fields like main.duration.
        if (!loop) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = loop;
        main.playOnAwake = loop;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.material = SharedMaterial();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.alignment = ParticleSystemRenderSpace.View;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    ParticleSystem MakeSmoke()
    {
        ParticleSystem ps = NewSystem("smoke", true);
        ParticleSystem.MainModule main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 1.5f;
        main.startSpeed = 1.3f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
        main.startColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        main.gravityModifier = -0.04f;
        main.maxParticles = 145;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 27f; // denser plume

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.35f;

        FadeAlpha(ps, 0.18f, 0.5f, new Color(0.25f, 0.25f, 0.25f));
        GrowSize(ps, 0.7f, 1.6f);
        return ps;
    }

    // One flame tuft. Several are placed around the car (centre + sides + rear)
    // so it reads as a vehicle on fire rather than one jet from the middle.
    ParticleSystem MakeFire(Vector3 localPos, float sizeScale, float rate)
    {
        ParticleSystem ps = NewSystem("fire", true);
        ps.transform.localPosition = localPos;

        ParticleSystem.MainModule main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 0.6f;
        main.startSpeed = 2.1f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.73f * sizeScale, 1.63f * sizeScale); // bigger flames
        main.startColor = new Color(1f, 0.55f, 0.12f, 1f);
        main.gravityModifier = -0.16f;
        main.maxParticles = 90;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 16f;
        sh.radius = 0.2f * Mathf.Max(0.4f, sizeScale);
        sh.rotation = new Vector3(-90f, 0f, 0f);

        FadeAlpha(ps, 0.1f, 1f, new Color(1f, 0.72f, 0.22f));
        GrowSize(ps, 1f, 0.3f);
        return ps;
    }

    void SetFires(bool on)
    {
        if (fires == null) return;
        for (int i = 0; i < fires.Length; i++) SetEmission(fires[i], on);
    }

    ParticleSystem MakeExplosion()
    {
        ParticleSystem ps = NewSystem("explosion", false);
        ParticleSystem.MainModule main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
        main.startColor = new Color(1f, 0.6f, 0.15f, 1f);
        main.maxParticles = 60;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.3f;

        FadeAlpha(ps, 0.15f, 1f, new Color(1f, 0.5f, 0.15f));
        GrowSize(ps, 1.2f, 0.25f);
        return ps;
    }

    // ---- helpers ----

    static void FadeAlpha(ParticleSystem ps, float inAt, float peak, Color tint)
    {
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, inAt), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    static void GrowSize(ParticleSystem ps, float from, float to)
    {
        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
    }

    static void SetEmission(ParticleSystem ps, bool on)
    {
        if (ps == null) return;
        ParticleSystem.EmissionModule em = ps.emission;
        if (em.enabled != on) em.enabled = on;
    }

    static Material SharedMaterial()
    {
        if (sharedMat != null) return sharedMat;
        sharedMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftDot() };
        return sharedMat;
    }

    static Texture2D SoftDot()
    {
        if (softDot != null) return softDot;
        const int size = 64;
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
