using UnityEngine;

// ATTACH THIS TO: a standalone object at a team's base/spawn (e.g.
// "PlayerBase"/"EnemyBase" for the normal arena; KnockoutManager builds its
// own pair for its bigger one). Purely a visual marker, no gameplay logic - a
// flat translucent circle on the ground showing where that team starts.
// Always on, in every mode - this is the same look Flag used to draw only for
// itself during Capture the Flag (its base-pad), generalised so every mode
// gets a base to start from and CTF's flag/capture-radius marker doubles as
// the same thing rather than a second, separate circle.
public class BaseMarker : MonoBehaviour
{
    public Color color = new Color(0.15f, 0.4f, 0.95f, 0.28f);
    public float radius = 3f;

    void Awake()
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "Pad";
        Destroy(pad.GetComponent<Collider>());
        pad.transform.SetParent(transform, false);
        pad.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        pad.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);

        MeshRenderer mr = pad.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.material = new Material(Shader.Find("Sprites/Default")) { color = color };
    }
}
