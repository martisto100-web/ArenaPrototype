using UnityEngine;
using UnityEngine.UI;

// ATTACH THIS TO: a joystick "bg" object (the one that also has VirtualJoystick).
// Reskins the background + handle Images at runtime as translucent circles, with
// a glyph baked in — directional arrows for the move stick, a bullet for the
// fire stick. All textures are generated in code (no art assets).
[RequireComponent(typeof(VirtualJoystick))]
[RequireComponent(typeof(Image))]
public class JoystickSkin : MonoBehaviour
{
    public enum Glyph { None, DirectionalArrows, Bullet }

    [Header("Glyph")]
    public Glyph glyph = Glyph.None;

    [Header("Opacity (0 = invisible, 1 = solid)")]
    [Range(0f, 1f)] public float backgroundOpacity = 0.3f;
    [Range(0f, 1f)] public float handleOpacity = 0.42f;
    [Range(0f, 1f)] public float backgroundGlyphOpacity = 0.22f;
    [Range(0f, 1f)] public float handleGlyphOpacity = 0.8f;

    [Header("Colour")]
    public Color ringColor = Color.white;
    public Color glyphColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    private const int TexSize = 256;

    void Start()
    {
        VirtualJoystick vj = GetComponent<VirtualJoystick>();
        Image bg = GetComponent<Image>();
        Image handle = (vj != null && vj.handle != null) ? vj.handle.GetComponent<Image>() : null;

        if (bg != null) Apply(bg, BuildTexture(backgroundOpacity, backgroundGlyphOpacity));
        if (handle != null) Apply(handle, BuildTexture(handleOpacity, handleGlyphOpacity));
    }

    void Apply(Image img, Texture2D tex)
    {
        img.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = Color.white;
    }

    // 2x supersampled, then boxed down 2x2 for cheap anti-aliasing.
    Texture2D BuildTexture(float discOpacity, float glyphOpacity)
    {
        int ss = TexSize * 2;
        Color[] hi = new Color[ss * ss];
        for (int y = 0; y < ss; y++)
        {
            for (int x = 0; x < ss; x++)
            {
                float nx = ((x + 0.5f) / ss - 0.5f) * 2f; // -1..1
                float ny = ((y + 0.5f) / ss - 0.5f) * 2f;
                hi[y * ss + x] = SamplePixel(nx, ny, discOpacity, glyphOpacity);
            }
        }

        Color[] lo = new Color[TexSize * TexSize];
        for (int y = 0; y < TexSize; y++)
        {
            for (int x = 0; x < TexSize; x++)
            {
                Color c = hi[(2 * y) * ss + 2 * x]
                        + hi[(2 * y) * ss + 2 * x + 1]
                        + hi[(2 * y + 1) * ss + 2 * x]
                        + hi[(2 * y + 1) * ss + 2 * x + 1];
                lo[y * TexSize + x] = c * 0.25f;
            }
        }

        Texture2D tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(lo);
        tex.Apply();
        return tex;
    }

    // nx,ny in -1..1 (image edge at magnitude 1). Straight-alpha colour out.
    Color SamplePixel(float nx, float ny, float discOpacity, float glyphOpacity)
    {
        float r = Mathf.Sqrt(nx * nx + ny * ny);
        Color col = new Color(ringColor.r, ringColor.g, ringColor.b, 0f);

        if (r <= 0.98f)
        {
            float a = (r >= 0.84f) ? Mathf.Min(1f, discOpacity * 2.2f) : discOpacity; // stronger rim ring
            col = new Color(ringColor.r, ringColor.g, ringColor.b, a);
        }

        if (glyph != Glyph.None && glyphOpacity > 0f)
        {
            bool inGlyph = glyph == Glyph.DirectionalArrows ? InArrows(nx, ny) : InBullet(nx, ny);
            if (inGlyph) col = Blend(col, new Color(glyphColor.r, glyphColor.g, glyphColor.b, glyphOpacity));
        }

        return col;
    }

    static Color Blend(Color dst, Color src)
    {
        float a = src.a + dst.a * (1f - src.a);
        if (a <= 0.0001f) return new Color(dst.r, dst.g, dst.b, 0f);
        float inv = 1f - src.a;
        return new Color(
            (src.r * src.a + dst.r * dst.a * inv) / a,
            (src.g * src.a + dst.g * dst.a * inv) / a,
            (src.b * src.a + dst.b * dst.a * inv) / a,
            a);
    }

    // ---- glyph shapes (nx,ny in -1..1) ----

    static bool InArrows(float nx, float ny)
    {
        return UpArrow(nx, ny) || UpArrow(-nx, -ny) || UpArrow(ny, -nx) || UpArrow(-ny, nx);
    }

    static bool UpArrow(float x, float y)
    {
        const float gap = 0.16f;
        const float shoulder = 0.42f;
        const float tip = 0.82f;
        const float shaftHalf = 0.10f;
        const float headHalf = 0.28f;

        if (y > gap && y <= shoulder && Mathf.Abs(x) <= shaftHalf) return true;
        if (y > shoulder && y <= tip)
        {
            float t = (tip - y) / (tip - shoulder); // 1 at shoulder -> 0 at tip
            if (Mathf.Abs(x) <= headHalf * t) return true;
        }
        return false;
    }

    static bool InBullet(float x, float y)
    {
        const float w = 0.26f;
        const float baseY = -0.62f;
        const float rimY = -0.54f;
        const float neckY = 0.16f;
        const float tipY = 0.74f;
        const float rimOverhang = 0.06f;

        if (y >= baseY && y <= rimY && Mathf.Abs(x) <= w + rimOverhang) return true;   // cartridge rim
        if (y > rimY && y <= neckY && Mathf.Abs(x) <= w) return true;                  // case
        if (y > neckY && y <= tipY)                                                    // ogive nose
        {
            float t = (y - neckY) / (tipY - neckY);
            if (Mathf.Abs(x) <= w * (1f - t * t)) return true;
        }
        return false;
    }
}
