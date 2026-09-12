using UnityEngine;

// ATTACH THIS TO: an empty GameObject at the arena centre (e.g. "ControlZone" -
// active only while Gridlock is the running mode, toggled by
// MatchModeManager). Builds its own glowing ground-disc at runtime (no art,
// same approach as DeathZone/Flag) and, every frame, checks which team(s)
// currently have a car standing inside (PlayerInside / EnemyInside) via a
// simple distance check - MatchModeManager reads these to drive each team's
// own, independent capture progress. Purely mechanical/visual: no scoring or
// win logic lives here.
//
// Neutral (nobody inside) glows a dim white/grey. One team inside tints the
// WHOLE disc that team's colour. Both inside splits it half red / half blue
// (a fixed left/right split - purely a status readout, not tied to where in
// the zone each car actually is). The disc constantly breathes brighter/dimmer
// (not a still fill), and a soft halo ring just outside its edge - baked as a
// vertex-alpha gradient from bright at the disc edge to nothing at glowWidth
// further out - pulses in time with it, so it actually reads as glowing rather
// than a flat coloured circle.
public class ControlZone : MonoBehaviour
{
    [Header("Zone")]
    public float radius = 6f;

    [Header("Colours")]
    public Color neutralColor = new Color(0.9f, 0.9f, 0.95f, 0.22f);
    public Color playerColor = new Color(0.15f, 0.4f, 0.95f, 0.5f);
    public Color enemyColor = new Color(0.85f, 0.15f, 0.12f, 0.5f);

    [Header("Glow / pulse")]
    public float pulseSpeed = 1.8f;
    [Range(0f, 0.6f)] public float pulseAmount = 0.35f;      // how much the fill's brightness/alpha breathes
    public float glowWidth = 1.8f;                            // halo ring extends this far past radius
    [Range(1f, 3f)] public float glowBrightness = 1.7f;       // how much brighter the halo's inner edge is than the fill

    public bool PlayerInside { get; private set; }
    public bool EnemyInside { get; private set; }

    private Vector3 center;
    private Material halfAMat;     // +X fill - solid team colour when only one team is present
    private Material halfBMat;     // -X fill
    private Material halfAGlowMat; // +X halo ring
    private Material halfBGlowMat; // -X halo ring

    void Awake()
    {
        center = transform.position;
        BuildVisual();
        UpdateVisual();
    }

    void OnEnable()
    {
        PlayerInside = false;
        EnemyInside = false;
        UpdateVisual();
    }

    void Update()
    {
        UpdateOccupancy();
        UpdateVisual();
    }

    void UpdateOccupancy()
    {
        bool p = false, e = false;
        foreach (TeamMember tm in FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            Health h = tm.GetComponent<Health>();
            if (h == null || h.IsDead) continue;

            Respawner r = tm.GetComponent<Respawner>();
            if (r != null && r.IsDead) continue; // dead / mid-drop-in doesn't count

            Vector3 pos = tm.transform.position;
            float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(center.x, center.z));
            if (d <= radius)
            {
                if (tm.team == Team.Player) p = true; else e = true;
            }
        }
        PlayerInside = p;
        EnemyInside = e;
    }

    void UpdateVisual()
    {
        Color a, b;
        if (PlayerInside && EnemyInside) { a = playerColor; b = enemyColor; }
        else if (PlayerInside) { a = b = playerColor; }
        else if (EnemyInside) { a = b = enemyColor; }
        else { a = b = neutralColor; }

        // Breathe: brightness and alpha rise/fall together, not just alpha -
        // a pure alpha fade against the ground just looks like it's fading in
        // and out, not glowing.
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        float brightPulse = 1f + (pulse - 1f) * 0.6f;

        if (halfAMat != null) halfAMat.color = ScaleAlpha(Brighten(a, brightPulse), pulse);
        if (halfBMat != null) halfBMat.color = ScaleAlpha(Brighten(b, brightPulse), pulse);

        // The halo's own mesh already carries a bright-at-the-disc-edge ->
        // transparent-at-glowWidth vertex gradient; this just tints/pulses it.
        if (halfAGlowMat != null) halfAGlowMat.color = GlowTint(a, pulse);
        if (halfBGlowMat != null) halfBGlowMat.color = GlowTint(b, pulse);
    }

    Color GlowTint(Color baseColor, float pulse)
    {
        Color c = Brighten(baseColor, glowBrightness);
        c.a = Mathf.Clamp01(baseColor.a * pulse);
        return c;
    }

    static Color Brighten(Color c, float mul)
    {
        return new Color(Mathf.Clamp01(c.r * mul), Mathf.Clamp01(c.g * mul), Mathf.Clamp01(c.b * mul), c.a);
    }

    static Color ScaleAlpha(Color c, float mul)
    {
        return new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a * mul));
    }

    // ---- runtime visual: two half-disc fills + two half-ring halos (left/right), each independently tintable ----

    void BuildVisual()
    {
        GameObject rig = new GameObject("ZoneVisual");
        rig.transform.SetParent(transform, false);
        rig.transform.localPosition = new Vector3(0f, 0.03f, 0f); // just above the Plane, avoids z-fighting

        halfAMat = BuildMesh(rig.transform, "HalfA", BuildHalfDiscMesh(-90f, 180f, radius, 32));
        halfBMat = BuildMesh(rig.transform, "HalfB", BuildHalfDiscMesh(90f, 180f, radius, 32));
        halfAGlowMat = BuildMesh(rig.transform, "HalfA_Glow", BuildHalfRingMesh(-90f, 180f, radius, radius + glowWidth, 32));
        halfBGlowMat = BuildMesh(rig.transform, "HalfB_Glow", BuildHalfRingMesh(90f, 180f, radius, radius + glowWidth, 32));
    }

    Material BuildMesh(Transform parent, string goName, Mesh mesh)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent, false);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mr.material = mat;
        return mat;
    }

    // A triangle fan from the centre out to an arc of sweepDeg degrees.
    static Mesh BuildHalfDiscMesh(float startAngleDeg, float sweepDeg, float r, int segments)
    {
        Vector3[] verts = new Vector3[segments + 2];
        int[] tris = new int[segments * 3];

        verts[0] = Vector3.zero;
        float startRad = startAngleDeg * Mathf.Deg2Rad;
        float sweepRad = sweepDeg * Mathf.Deg2Rad;

        for (int i = 0; i <= segments; i++)
        {
            float angle = startRad + sweepRad * (i / (float)segments);
            verts[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
        }

        for (int i = 0; i < segments; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        Mesh m = new Mesh { name = "ZoneHalf" };
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // A half-annulus from innerR to outerR, white vertex colour graded from
    // fully opaque at the inner edge to fully transparent at the outer edge -
    // the soft glow falloff. Material.color tints/dims the whole thing.
    static Mesh BuildHalfRingMesh(float startAngleDeg, float sweepDeg, float innerR, float outerR, int segments)
    {
        Vector3[] verts = new Vector3[(segments + 1) * 2];
        Color[] cols = new Color[(segments + 1) * 2];
        int[] tris = new int[segments * 6];

        float startRad = startAngleDeg * Mathf.Deg2Rad;
        float sweepRad = sweepDeg * Mathf.Deg2Rad;

        for (int i = 0; i <= segments; i++)
        {
            float angle = startRad + sweepRad * (i / (float)segments);
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            verts[i * 2] = dir * innerR;
            verts[i * 2 + 1] = dir * outerR;
            cols[i * 2] = new Color(1f, 1f, 1f, 1f);
            cols[i * 2 + 1] = new Color(1f, 1f, 1f, 0f);
        }

        for (int i = 0; i < segments; i++)
        {
            int in0 = i * 2, out0 = i * 2 + 1, in1 = (i + 1) * 2, out1 = (i + 1) * 2 + 1;
            int t = i * 6;
            tris[t + 0] = in0; tris[t + 1] = out0; tris[t + 2] = in1;
            tris[t + 3] = out0; tris[t + 4] = out1; tris[t + 5] = in1;
        }

        Mesh m = new Mesh { name = "ZoneGlowRing" };
        m.vertices = verts;
        m.colors = cols;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
