using UnityEngine;

// ATTACH THIS TO: the GameDirector object (Deathmatch/CTF's instance), or a
// standalone object with its own tuning (Knockout's — see KnockoutManager).
// Sudden-death hazard: when called, a circular "safe" ring shrinks from
// covering the whole arena down to nothing, and any car caught outside it
// takes escalating damage per second. Both the shrink SPEED and the damage
// use the same "stage" shape - a list of (elapsed-seconds, value) pairs, the
// value holding at the last one reached forever after - so a constant shrink
// (Deathmatch: one stage) and an escalating one (Knockout: several) are just
// different data on the same mechanism; see shrinkRateStageTimes/Rates and
// stageTimes/stageDamage. The visual is a translucent, fiery orange annulus
// covering everything beyond the safe ring, plus a ring of ember particles
// riding the shrinking edge — all built at runtime, no art, same approach as
// DamageFx/HealthBar. Loosely modelled on battle-royale "storm" zones
// (Fortnite/PUBG/Apex) but reskinned fiery/transparent for this arena rather
// than copying any one game's look.
public class DeathZone : MonoBehaviour
{
    [Header("Shrink")]
    public Vector3 center = Vector3.zero;
    public float startRadius = 36f;    // big enough to clear the 50x50 plane's corners (~35.4)
    public float outerRadius = 45f;    // the annulus's fixed outer edge - just needs to clear the plane

    [Header("Shrink rate stages (seconds since BeginShrinking -> radius units/sec)")]
    public float[] shrinkRateStageTimes = { 0f };    // Deathmatch: one constant stage, startRadius/20s
    public float[] shrinkRateStages = { 1.8f };

    [Header("Damage stages (seconds elapsed -> damage per second)")]
    public float[] stageTimes = { 0f, 5f, 10f, 15f };
    public float[] stageDamage = { 1f, 3f, 5f, 7f };

    [Header("Visual")]
    public Color zoneColor = new Color(0.9f, 0.22f, 0.04f, 0.35f); // deep translucent red-orange, outer edge
    public Color edgeColor = new Color(1f, 0.75f, 0.15f, 0.85f);   // bright fiery yellow-orange, safe-ring edge
    public int ringSegments = 64;
    public int emberCount = 20;
    public float pulseSpeed = 2.5f;

    public bool Active { get; private set; }
    public float CurrentSafeRadius { get; private set; }

    private float elapsed;
    private GameObject visualRoot;
    private Mesh annulusMesh;
    private Vector3[] verts;
    private Material zoneMat;
    private ParticleSystem[] embers;

    private static Material sharedEmberMat;
    private static Texture2D softDot;

    void Awake()
    {
        BuildVisual();
        SetVisible(false);
    }

    public void BeginShrinking()
    {
        elapsed = 0f;
        CurrentSafeRadius = startRadius;
        Active = true;
        SetVisible(true);
        UpdateVisual();
    }

    public void Stop()
    {
        Active = false;
        SetVisible(false);
    }

    void Update()
    {
        if (!Active) return;

        elapsed += Time.deltaTime;
        float rate = StageValue(shrinkRateStageTimes, shrinkRateStages, 1.8f);
        CurrentSafeRadius = Mathf.Max(0f, CurrentSafeRadius - rate * Time.deltaTime);
        UpdateVisual();
        ApplyDamage();
    }

    float CurrentDamagePerSecond() => StageValue(stageTimes, stageDamage, 1f);

    // Shared by the shrink-rate and damage schedules: whichever stage's time
    // has most recently been crossed wins, holding at the last one forever
    // once elapsed runs past it.
    float StageValue(float[] times, float[] values, float fallback)
    {
        float result = values.Length > 0 ? values[0] : fallback;
        for (int i = 0; i < times.Length && i < values.Length; i++)
        {
            if (elapsed >= times[i]) result = values[i];
        }
        return result;
    }

    void ApplyDamage()
    {
        float dps = CurrentDamagePerSecond();
        foreach (TeamMember tm in FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            Health h = tm.GetComponent<Health>();
            if (h == null || h.IsDead) continue;

            Respawner r = tm.GetComponent<Respawner>();
            if (r != null && r.IsDead) continue; // dead / mid-drop-in - the zone leaves it alone

            Vector3 p = tm.transform.position;
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(center.x, center.z));
            if (d > CurrentSafeRadius)
            {
                h.TakeDamage(dps * Time.deltaTime);
            }
        }
    }

    void SetVisible(bool on)
    {
        if (visualRoot != null) visualRoot.SetActive(on);
    }

    // ---- runtime visual ----

    void BuildVisual()
    {
        visualRoot = new GameObject("DeathZoneVisual");
        visualRoot.transform.SetParent(transform, false);
        visualRoot.transform.position = new Vector3(center.x, 0.05f, center.z);

        GameObject meshGO = new GameObject("Annulus");
        meshGO.transform.SetParent(visualRoot.transform, false);
        MeshFilter mf = meshGO.AddComponent<MeshFilter>();
        MeshRenderer mr = meshGO.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        zoneMat = new Material(Shader.Find("Sprites/Default"));
        mr.material = zoneMat;

        annulusMesh = new Mesh { name = "DeathZoneAnnulus" };
        mf.mesh = annulusMesh;
        BuildAnnulusTopology();

        embers = new ParticleSystem[Mathf.Max(0, emberCount)];
        for (int i = 0; i < embers.Length; i++)
        {
            embers[i] = MakeEmber(visualRoot.transform);
        }
    }

    void BuildAnnulusTopology()
    {
        int segs = Mathf.Max(8, ringSegments);
        verts = new Vector3[segs * 2];
        Color[] cols = new Color[segs * 2];
        Vector3[] normals = new Vector3[segs * 2];
        Vector2[] uvs = new Vector2[segs * 2];
        int[] tris = new int[segs * 6];

        for (int i = 0; i < segs; i++)
        {
            float angle = i / (float)segs * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            verts[i * 2] = dir * outerRadius;     // outer ring - fixed
            verts[i * 2 + 1] = dir * startRadius; // inner ring - rewritten every frame while active
            cols[i * 2] = zoneColor;
            cols[i * 2 + 1] = edgeColor;
            normals[i * 2] = Vector3.up;
            normals[i * 2 + 1] = Vector3.up;
        }

        for (int i = 0; i < segs; i++)
        {
            int next = (i + 1) % segs;
            int o0 = i * 2, in0 = i * 2 + 1, o1 = next * 2, in1 = next * 2 + 1;
            int t = i * 6;
            tris[t + 0] = o0; tris[t + 1] = in0; tris[t + 2] = o1;
            tris[t + 3] = in0; tris[t + 4] = in1; tris[t + 5] = o1;
        }

        annulusMesh.Clear();
        annulusMesh.vertices = verts;
        annulusMesh.colors = cols;
        annulusMesh.normals = normals;
        annulusMesh.uv = uvs;
        annulusMesh.triangles = tris;
    }

    void UpdateVisual()
    {
        int segs = Mathf.Max(8, ringSegments);
        for (int i = 0; i < segs; i++)
        {
            float angle = i / (float)segs * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            verts[i * 2 + 1] = dir * CurrentSafeRadius;
        }
        annulusMesh.vertices = verts;
        annulusMesh.RecalculateBounds();

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * pulseSpeed);
        zoneMat.color = new Color(1f, 1f, 1f, pulse); // multiplies the baked-in vertex alpha for a fiery flicker

        for (int i = 0; i < embers.Length; i++)
        {
            float angle = i / (float)embers.Length * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            embers[i].transform.localPosition = dir * CurrentSafeRadius;
        }
    }

    ParticleSystem MakeEmber(Transform parent)
    {
        GameObject go = new GameObject("ember");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.5f;
        main.startSpeed = 1.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startColor = new Color(1f, 0.5f, 0.1f, 1f);
        main.gravityModifier = -0.1f;
        main.maxParticles = 40;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 14f;

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 12f;
        sh.radius = 0.25f;
        sh.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0f), new GradientColorKey(new Color(1f, 0.2f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.material = SharedEmberMaterial();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        return ps;
    }

    static Material SharedEmberMaterial()
    {
        if (sharedEmberMat != null) return sharedEmberMat;
        sharedEmberMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftDot() };
        return sharedEmberMat;
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
