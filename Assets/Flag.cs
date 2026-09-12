using System;
using UnityEngine;

// ATTACH THIS TO: an empty GameObject sitting at each team's base (e.g.
// "BlueFlag" near the Wraith's spawn, "RedFlag" near the EnemyCar's) — placed by
// MatchModeManager's Capture The Flag setup. Builds its own pole+cloth visual
// and a flat base-pad marker at runtime (no art assets, same approach as
// HealthBar/DamageFx). MatchModeManager wires the two Flags together with
// SetOther() and toggles them active only during Capture the Flag.
//
// Only the OPPOSING team can pick this flag up (drive into it). Carrying it
// cuts the carrier's CarController.speedMultiplier by carrierSpeedPenalty until
// it's scored or the carrier dies — death sends the flag straight home (no
// ground-drop pickup, keeps the loop fast like the rest of this game's
// no-dead-time respawns). Driving the flag within captureRadius of the
// carrier's OWN base scores; MatchModeManager listens for Captured.
[RequireComponent(typeof(SphereCollider))]
public class Flag : MonoBehaviour
{
    [Header("Identity")]
    public Team owningTeam = Team.Player; // the team this flag belongs to / whose base it sits at

    [Header("Pickup / capture")]
    public float pickupRadius = 1.6f;
    public float captureRadius = 3f;
    [Range(0f, 1f)] public float carrierSpeedPenalty = 0.35f; // shaved off CarController.moveSpeed while carried
    public float recaptureLockout = 5f;                       // seconds this flag can't be taken again after a capture

    [Header("Visual")]
    public Vector3 carryOffset = new Vector3(0f, 2.4f, 0f);
    public float waveSpeed = 3f;
    public float waveAngle = 12f;

    public event Action<Team> Captured; // fires with the SCORING team

    public bool IsHome => carrier == null;
    public bool IsCarried => carrier != null;

    private Vector3 homePos;
    private Quaternion homeRot;
    private Transform carrier;
    private CarController carrierCar;
    private Team carrierTeam;
    private float lockedUntil;
    private Flag otherFlag;

    private Transform cloth;
    private GameObject basePad;

    void Awake()
    {
        homePos = transform.position;
        homeRot = transform.rotation;

        SphereCollider col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = pickupRadius;

        BuildVisual();
    }

    void OnEnable()
    {
        // Coming back from a mode switch: make sure it's sitting at its base,
        // not wherever it was frozen mid-carry when the flag got disabled.
        transform.SetPositionAndRotation(homePos, homeRot);
        carrier = null;
        carrierCar = null;
        lockedUntil = 0f;
    }

    void OnDisable()
    {
        if (carrierCar != null) carrierCar.speedMultiplier = 1f;
        carrier = null;
        carrierCar = null;
    }

    // Wired up once by MatchModeManager after both Flags exist.
    public void SetOther(Flag f) => otherFlag = f;

    void Update()
    {
        if (carrier == null) return;

        transform.position = carrier.position + carryOffset;
        transform.rotation = carrier.rotation;
        if (cloth != null)
        {
            float wave = Mathf.Sin(Time.time * waveSpeed) * waveAngle;
            cloth.localRotation = Quaternion.Euler(0f, wave, 0f);
        }

        if (otherFlag == null) return;
        if (Vector3.Distance(carrier.position, otherFlag.homePos) <= captureRadius)
        {
            Capture();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (carrier != null) return;          // already taken
        if (Time.time < lockedUntil) return;   // just captured, on cooldown

        TeamMember tm = other.GetComponentInParent<TeamMember>();
        if (tm == null || tm.team == owningTeam) return; // only the opposing team can take it

        Respawner resp = other.GetComponentInParent<Respawner>();
        if (resp != null && resp.IsDead) return; // dead / mid-drop-in car can't pick up

        CarController car = other.GetComponentInParent<CarController>();
        if (car == null) return;

        carrier = car.transform;
        carrierCar = car;
        carrierTeam = tm.team;
        carrierCar.speedMultiplier = 1f - carrierSpeedPenalty;
    }

    void Capture()
    {
        Team scoringTeam = carrierTeam;
        ReturnHome(applyLockout: true);
        Captured?.Invoke(scoringTeam);
    }

    // Instantly sends the flag back to its own base. Used on a capture, and by
    // MatchModeManager when the carrying car dies mid-run.
    public void ReturnHome(bool applyLockout)
    {
        if (carrierCar != null) carrierCar.speedMultiplier = 1f;
        carrier = null;
        carrierCar = null;
        transform.SetPositionAndRotation(homePos, homeRot);
        if (cloth != null) cloth.localRotation = Quaternion.identity;
        if (applyLockout) lockedUntil = Time.time + recaptureLockout;
    }

    // MatchModeManager calls this from every car-death handler; it's a no-op
    // unless that car happens to be the one currently carrying this flag.
    public void DropIfCarriedBy(Transform car)
    {
        if (carrier == car) ReturnHome(applyLockout: false);
    }

    // ---- runtime visual: pole + cloth + a flat base-pad marking the capture zone ----

    void BuildVisual()
    {
        Color flagColor = owningTeam == Team.Enemy ? new Color(0.85f, 0.15f, 0.12f) : new Color(0.15f, 0.4f, 0.95f);

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        Destroy(pole.GetComponent<Collider>());
        pole.transform.SetParent(transform, false);
        pole.transform.localScale = new Vector3(0.12f, 1.4f, 0.12f);
        pole.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        MeshRenderer poleR = pole.GetComponent<MeshRenderer>();
        poleR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        poleR.receiveShadows = false;
        poleR.material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.55f, 0.55f, 0.55f) };

        GameObject clothGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        clothGO.name = "Cloth";
        Destroy(clothGO.GetComponent<Collider>());
        clothGO.transform.SetParent(transform, false); // sibling of the pole, not a child - keeps its scale simple
        clothGO.transform.localScale = new Vector3(0.9f, 0.55f, 0.05f);
        clothGO.transform.localPosition = new Vector3(0.5f, 2.35f, 0f);
        MeshRenderer clothR = clothGO.GetComponent<MeshRenderer>();
        clothR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        clothR.receiveShadows = false;
        clothR.material = new Material(Shader.Find("Sprites/Default")) { color = flagColor };
        cloth = clothGO.transform;

        basePad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basePad.name = "BasePad";
        Destroy(basePad.GetComponent<Collider>());
        basePad.transform.position = homePos + Vector3.up * 0.02f;
        basePad.transform.localScale = new Vector3(captureRadius * 2f, 0.02f, captureRadius * 2f);
        MeshRenderer padR = basePad.GetComponent<MeshRenderer>();
        padR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        padR.receiveShadows = false;
        padR.material = new Material(Shader.Find("Sprites/Default")) { color = new Color(flagColor.r, flagColor.g, flagColor.b, 0.28f) };
    }

    void OnDestroy()
    {
        if (basePad != null) Destroy(basePad);
    }
}
