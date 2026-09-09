using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ATTACH THIS TO: the GameDirector.
// Shows an arrow at the screen edge for every combatant that is currently off
// camera, pointing toward it. Colour is relative to the local player:
//   other team -> red (enemy), same team -> yellow (ally).
// Builds its own overlay canvas and a pool of code-drawn arrow Images.
public class OffscreenMarkers : MonoBehaviour
{
    [Header("Colours")]
    public Color enemyColor = new Color(0.95f, 0.2f, 0.16f, 0.95f);
    public Color allyColor = new Color(1f, 0.84f, 0.16f, 0.95f);

    [Header("Layout (reference: 1920x1080)")]
    public float arrowSize = 62f;
    public float edgeMargin = 64f;
    public string playerTag = "Player";

    [Header("Refresh")]
    public float rescanInterval = 0.5f;

    private const float RefW = 1920f;
    private const float RefH = 1080f;

    private Camera cam;
    private Canvas canvas;
    private Sprite arrowSprite;
    private readonly List<Image> pool = new List<Image>();

    private Transform player;
    private Team playerTeam = Team.Player;
    private readonly List<TeamMember> combatants = new List<TeamMember>();
    private float rescanTimer;

    void Awake()
    {
        cam = Camera.main;
        BuildCanvas();
        arrowSprite = BuildArrowSprite();
        Rescan();
    }

    void BuildCanvas()
    {
        GameObject go = new GameObject("OffscreenMarkerCanvas");
        go.transform.SetParent(transform, false);

        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.matchWidthOrHeight = 0.5f;
    }

    void Rescan()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null)
            {
                player = p.transform;
                TeamMember ptm = p.GetComponent<TeamMember>();
                if (ptm != null) playerTeam = ptm.team;
            }
        }

        combatants.Clear();
        foreach (TeamMember tm in FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            if (player != null && tm.transform == player) continue;
            combatants.Add(tm);
        }
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;

        rescanTimer -= Time.deltaTime;
        if (rescanTimer <= 0f) { Rescan(); rescanTimer = rescanInterval; }

        if (cam == null) { HideFrom(0); return; }

        float halfX = 0.5f - edgeMargin / RefW;
        float halfY = 0.5f - edgeMargin / RefH;

        int used = 0;
        for (int i = 0; i < combatants.Count; i++)
        {
            TeamMember tm = combatants[i];
            if (tm == null) continue;

            Respawner rs = tm.GetComponent<Respawner>();
            if (rs != null && rs.IsDead) continue;

            Vector3 vp = cam.WorldToViewportPoint(tm.transform.position + Vector3.up);
            bool behind = vp.z < 0f;
            Vector2 c = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (behind) c = -c;

            bool onScreen = !behind && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
            if (onScreen) continue;

            if (c.sqrMagnitude < 1e-6f) c = new Vector2(0f, -0.5f);
            float scale = Mathf.Min(halfX / Mathf.Max(Mathf.Abs(c.x), 1e-4f),
                                    halfY / Mathf.Max(Mathf.Abs(c.y), 1e-4f));
            Vector2 edge = c * scale;

            Image arrow = GetArrow(used++);
            arrow.rectTransform.anchoredPosition = new Vector2(edge.x * RefW, edge.y * RefH);
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(c.y, c.x) * Mathf.Rad2Deg - 90f);
            arrow.color = (tm.team == playerTeam) ? allyColor : enemyColor;
        }

        HideFrom(used);
    }

    Image GetArrow(int index)
    {
        while (pool.Count <= index)
        {
            GameObject go = new GameObject("marker", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            Image img = go.AddComponent<Image>();
            img.sprite = arrowSprite;
            img.raycastTarget = false;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(arrowSize, arrowSize);

            Outline ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.8f);
            ol.effectDistance = new Vector2(2.5f, -2.5f);

            pool.Add(img);
        }

        pool[index].gameObject.SetActive(true);
        return pool[index];
    }

    void HideFrom(int index)
    {
        for (int i = index; i < pool.Count; i++)
        {
            if (pool[i] != null) pool[i].gameObject.SetActive(false);
        }
    }

    Sprite BuildArrowSprite()
    {
        const int size = 128;
        int ss = size * 2;

        Color[] hi = new Color[ss * ss];
        for (int y = 0; y < ss; y++)
        {
            for (int x = 0; x < ss; x++)
            {
                float nx = ((x + 0.5f) / ss - 0.5f) * 2f;
                float ny = ((y + 0.5f) / ss - 0.5f) * 2f;
                hi[y * ss + x] = InArrow(nx, ny) ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }

        Color[] lo = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                lo[y * size + x] = (hi[(2 * y) * ss + 2 * x]
                                  + hi[(2 * y) * ss + 2 * x + 1]
                                  + hi[(2 * y + 1) * ss + 2 * x]
                                  + hi[(2 * y + 1) * ss + 2 * x + 1]) * 0.25f;
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(lo);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Arrowhead pointing +Y, with a short tail. nx,ny in -1..1.
    static bool InArrow(float nx, float ny)
    {
        if (ny <= 0.85f && ny >= -0.15f)
        {
            float t = (0.85f - ny) / 1.0f; // 0 at tip -> 1 at head base
            if (Mathf.Abs(nx) <= 0.9f * t) return true;
        }
        if (ny < -0.15f && ny >= -0.7f && Mathf.Abs(nx) <= 0.26f) return true;
        return false;
    }
}
