using UnityEngine;

// ATTACH THIS TO: a car root that has a Health component.
// Builds a small world-space bar a couple of metres above the car at runtime,
// billboarded to the camera, green when full -> red when low. No scene setup
// or art assets needed; it is created in code and cleaned up with the car.
public class HealthBar : MonoBehaviour
{
    [Header("Placement (world units)")]
    public Vector3 worldOffset = new Vector3(0f, 2.9f, 0f);
    public Vector2 size = new Vector2(2.7f, 0.42f);
    public float border = 0.05f;

    // Set by Respawner to hide the bar while the car is dead / dropping in.
    public bool Hidden { get; set; }

    [Header("Colours")]
    public Color backColor = new Color(0f, 0f, 0f, 0.65f);
    public Color fullColor = new Color(0.25f, 0.85f, 0.3f, 1f);
    public Color lowColor = new Color(0.9f, 0.15f, 0.15f, 1f);

    private Health health;
    private Camera cam;
    private Transform root;
    private Transform fill;
    private Material fillMat;
    private float maxFillWidth;

    void Awake()
    {
        health = GetComponentInParent<Health>();
        cam = Camera.main;
        Build();
    }

    void Build()
    {
        root = new GameObject(name + "_HealthBar").transform;

        Transform bg = MakeQuad("bg", backColor);
        bg.localScale = new Vector3(size.x, size.y, 1f);

        fill = MakeQuad("fill", fullColor);
        maxFillWidth = Mathf.Max(0.01f, size.x - border * 2f);
        fill.localScale = new Vector3(maxFillWidth, Mathf.Max(0.01f, size.y - border * 2f), 1f);
        fill.localPosition = new Vector3(0f, 0f, -0.01f);
        fillMat = fill.GetComponent<MeshRenderer>().material;
    }

    Transform MakeQuad(string quadName, Color color)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = quadName;

        Collider col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        quad.transform.SetParent(root, false);

        MeshRenderer r = quad.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.material = new Material(Shader.Find("Sprites/Default")) { color = color };
        return quad.transform;
    }

    void LateUpdate()
    {
        if (health == null || root == null) return;

        bool show = !Hidden;
        if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        if (!show) return;

        root.position = transform.position + worldOffset;
        if (cam == null) cam = Camera.main;
        if (cam != null) root.rotation = cam.transform.rotation;

        float frac = Mathf.Clamp01(health.HealthFraction);
        float w = Mathf.Max(0.0001f, maxFillWidth * frac);
        fill.localScale = new Vector3(w, fill.localScale.y, 1f);
        fill.localPosition = new Vector3(-(maxFillWidth - w) * 0.5f, 0f, -0.01f);
        if (fillMat != null) fillMat.color = Color.Lerp(lowColor, fullColor, frac);
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }
}
