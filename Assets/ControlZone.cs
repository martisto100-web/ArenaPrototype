using UnityEngine;

// ATTACH THIS TO: an empty GameObject at the arena centre (e.g. "ControlZone" -
// active only while Zone Control is the running mode, toggled by
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
// the zone each car actually is).
public class ControlZone : MonoBehaviour
{
    [Header("Zone")]
    public float radius = 6f;

    [Header("Colours")]
    public Color neutralColor = new Color(0.9f, 0.9f, 0.95f, 0.22f);
    public Color playerColor = new Color(0.15f, 0.4f, 0.95f, 0.5f);
    public Color enemyColor = new Color(0.85f, 0.15f, 0.12f, 0.5f);
    public float pulseSpeed = 1.6f;
    [Range(0f, 0.5f)] public float pulseAmount = 0.18f;

    public bool PlayerInside { get; private set; }
    public bool EnemyInside { get; private set; }

    private Vector3 center;
    private Material halfAMat; // +X side - solid team colour when only one team is present
    private Material halfBMat; // -X side

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

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        if (halfAMat != null) halfAMat.color = ScaleAlpha(a, pulse);
        if (halfBMat != null) halfBMat.color = ScaleAlpha(b, pulse);
    }

    static Color ScaleAlpha(Color c, float mul)
    {
        return new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a * mul));
    }

    // ---- runtime visual: two half-disc meshes (left/right) so each can be tinted independently ----

    void BuildVisual()
    {
        GameObject rig = new GameObject("ZoneVisual");
        rig.transform.SetParent(transform, false);
        rig.transform.localPosition = new Vector3(0f, 0.03f, 0f); // just above the Plane, avoids z-fighting

        halfAMat = BuildHalf(rig.transform, "HalfA", -90f, 180f);
        halfBMat = BuildHalf(rig.transform, "HalfB", 90f, 180f);
    }

    Material BuildHalf(Transform parent, string goName, float startAngleDeg, float sweepDeg)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent, false);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.mesh = BuildHalfDiscMesh(startAngleDeg, sweepDeg, 32);

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mr.material = mat;
        return mat;
    }

    // A triangle fan from the centre out to an arc of sweepDeg degrees.
    Mesh BuildHalfDiscMesh(float startAngleDeg, float sweepDeg, int segments)
    {
        Vector3[] verts = new Vector3[segments + 2];
        int[] tris = new int[segments * 3];

        verts[0] = Vector3.zero;
        float startRad = startAngleDeg * Mathf.Deg2Rad;
        float sweepRad = sweepDeg * Mathf.Deg2Rad;

        for (int i = 0; i <= segments; i++)
        {
            float angle = startRad + sweepRad * (i / (float)segments);
            verts[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
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
}
