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
// it's scored or the carrier dies. On death it drops right where the carrier
// died and sits there, inert, for dropDuration seconds - blinking faster and
// faster as that clock runs out - then teleports home on its own (no lockout).
// Driving the flag within captureRadius of the carrier's OWN base scores;
// MatchModeManager listens for Captured.
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

    [Header("Drop on carrier death")]
    public float dropDuration = 6f;        // seconds it sits where the carrier died before auto-returning
    public float blinkStartInterval = 0.5f; // seconds between blinks right after dropping (slow)
    public float blinkEndInterval = 0.08f;  // seconds between blinks right before it vanishes home (fast)

    [Header("Visual")]
    public Vector3 carryOffset = new Vector3(0f, 2.4f, 0f);
    public float waveSpeed = 3f;
    public float waveAngle = 12f;

    public event Action<Team> Captured; // fires with the SCORING team

    public bool IsHome => carrier == null && !dropped;
    public bool IsCarried => carrier != null;

    private Vector3 homePos;
    private Quaternion homeRot;
    private Transform carrier;
    private CarController carrierCar;
    private Team carrierTeam;
    private float lockedUntil;
    private Flag otherFlag;

    private bool dropped;
    private float dropElapsed;
    private float blinkTimer;
    private bool visualVisible = true;

    private GameObject visual; // pole + cloth - toggled for the drop-blink
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
        // not wherever it was frozen mid-carry/mid-drop when the flag got disabled.
        transform.SetPositionAndRotation(homePos, homeRot);
        carrier = null;
        carrierCar = null;
        dropped = false;
        lockedUntil = 0f;
        SetVisualVisible(true);
    }

    void OnDisable()
    {
        if (carrierCar != null) carrierCar.speedMultiplier = 1f;
        carrier = null;
        carrierCar = null;
        dropped = false;
        SetVisualVisible(true);
    }

    // Wired up once by MatchModeManager after both Flags exist.
    public void SetOther(Flag f) => otherFlag = f;

    void Update()
    {
        if (dropped)
        {
            UpdateDropped();
            return;
        }

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

    // Sits inert where the carrier died, blinking faster as dropDuration runs
    // out, then teleports itself home (no lockout - the enemy just has to run
    // the whole way again).
    void UpdateDropped()
    {
        dropElapsed += Time.deltaTime;
        if (dropElapsed >= dropDuration)
        {
            dropped = false;
            ReturnHome(applyLockout: false);
            return;
        }

        float k = Mathf.Clamp01(dropElapsed / dropDuration);
        float halfPeriod = Mathf.Lerp(blinkStartInterval, blinkEndInterval, k);

        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f)
        {
            visualVisible = !visualVisible;
            SetVisualVisible(visualVisible);
            blinkTimer = halfPeriod;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (carrier != null) return;          // already taken
        if (dropped) return;                  // sitting inert on the ground, waiting to auto-return
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

    // Sends the flag back to its own base. Used on a capture, and once a
    // ground-dropped flag's timer runs out.
    public void ReturnHome(bool applyLockout)
    {
        if (carrierCar != null) carrierCar.speedMultiplier = 1f;
        carrier = null;
        carrierCar = null;
        dropped = false;
        SetVisualVisible(true);
        visualVisible = true;
        transform.SetPositionAndRotation(homePos, homeRot);
        if (cloth != null) cloth.localRotation = Quaternion.identity;
        if (applyLockout) lockedUntil = Time.time + recaptureLockout;
    }

    // MatchModeManager calls this from every car-death handler; it's a no-op
    // unless that car happens to be the one currently carrying this flag. Drops
    // it right where that car died instead of sending it home immediately -
    // UpdateDropped() takes it from here.
    public void DropIfCarriedBy(Transform car)
    {
        if (carrier != car) return;

        if (carrierCar != null) carrierCar.speedMultiplier = 1f;
        carrier = null;
        carrierCar = null;

        transform.SetPositionAndRotation(car.position, Quaternion.identity);
        if (cloth != null) cloth.localRotation = Quaternion.identity;

        dropped = true;
        dropElapsed = 0f;
        blinkTimer = blinkStartInterval;
        visualVisible = true;
        SetVisualVisible(true);
    }

    // ---- runtime visual: pole + cloth + a flat base-pad marking the capture zone ----

    void BuildVisual()
    {
        Color flagColor = owningTeam == Team.Enemy ? new Color(0.85f, 0.15f, 0.12f) : new Color(0.15f, 0.4f, 0.95f);

        visual = new GameObject("Visual");
        visual.transform.SetParent(transform, false);

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        Destroy(pole.GetComponent<Collider>());
        pole.transform.SetParent(visual.transform, false);
        pole.transform.localScale = new Vector3(0.12f, 1.4f, 0.12f);
        pole.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        MeshRenderer poleR = pole.GetComponent<MeshRenderer>();
        poleR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        poleR.receiveShadows = false;
        poleR.material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.55f, 0.55f, 0.55f) };

        GameObject clothGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        clothGO.name = "Cloth";
        Destroy(clothGO.GetComponent<Collider>());
        clothGO.transform.SetParent(visual.transform, false); // sibling of the pole, not a child - keeps its scale simple
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

    void SetVisualVisible(bool on)
    {
        if (visual != null) visual.SetActive(on);
    }

    void OnDestroy()
    {
        if (basePad != null) Destroy(basePad);
    }
}
