using UnityEngine;

// ATTACH THIS TO: your vehicle (same as before - no need to re-add).
// The aim direction points a "turret" independently of the car's facing, and
// firing happens automatically while aim is pushed past fireDeadzone.
// Input source, in priority order: an IVehicleInput component on this object
// (player joystick rig or EnemyDriverAI) -> the assigned aimJoystick ->
// "hold Ctrl / left mouse to fire straight ahead" for keyboard testing.
// Spawned bullets/rockets are stamped with this vehicle's Team so they don't
// hit allies.
//
// weaponType picks between two loadouts sharing everything above (same aim
// input, same fireDeadzone, same immediate-fire-then-refire-at-fireRate
// trigger feel):
//   MachineGun     - fires projectilePrefab at fireRate. Every shot plays one
//                     discrete gunshot sample (fireShots) - no baked "spray"
//                     loop; holding the trigger just retriggers shots at
//                     fireRate. Each sample is a real .50-cal shot with a
//                     ~1.3s decay tail, on a pool of 2D AudioSources so the
//                     tails of consecutive shots overlap and build the "wall"
//                     during a spray while a single tap still rings out
//                     fully. Per shot picks a random clip (never the same one
//                     twice running) with small pitch/volume jitter. Clips
//                     auto-load from Resources/Audio/Fire if left empty.
//   RocketLauncher - fires rocketPrefab (Rocket.cs) at rocketFireRate,
//                     reusing the same fireShots audio as a placeholder until
//                     a dedicated launch sound exists. At the instant a
//                     rocket is fired, FindRocketTarget scans for the
//                     nearest-to-centre living enemy within
//                     rocketTargetRange/rocketTargetConeHalfAngle of the
//                     current aim direction with line of sight - so pointing
//                     roughly at an enemy when you fire sends that shot
//                     homing after them; nothing in the cone just fires
//                     straight, so aiming with nobody nearby is never a
//                     wasted shot. There's no build-up/lock delay - it's
//                     decided fresh every time Shoot() runs.
public class Weapon : MonoBehaviour
{
    public enum WeaponType { MachineGun, RocketLauncher }

    [Header("Weapon Type")]
    public WeaponType weaponType = WeaponType.MachineGun;

    [Header("Machine Gun")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float fireRate = 0.25f;

    [Header("Rocket Launcher")]
    public GameObject rocketPrefab;
    public float rocketFireRate = 2f;
    public float rocketTargetRange = 30f;
    public float rocketTargetConeHalfAngle = 18f; // how far off dead-centre a target can be and still get picked

    [Header("Touch Input (optional)")]
    public VirtualJoystick aimJoystick;
    public float fireDeadzone = 0.3f;

    [Header("Audio")]
    public AudioClip[] fireShots;                          // one played per shot; empty -> Resources/Audio/Fire
    [Range(0f, 1f)] public float fireVolume = 0.5f;
    [Range(0.5f, 1.5f)] public float firePitch = 1f;       // gun tone; below 1 = deeper / bigger
    [Range(0f, 0.4f)] public float firePitchJitter = 0.03f; // clips are already pitch-varied; keep this subtle
    [Range(0f, 0.5f)] public float fireVolumeJitter = 0.12f;

    // false = normal. Set true to silence firing without touching `enabled` -
    // Respawner re-enables this component on landing regardless of who else
    // wants it held off (a post-match cinematic, a Knockout round countdown),
    // so callers that need firing to actually stay off use this instead.
    [System.NonSerialized] public bool inputLocked = false;

    private float cooldown = 0f;
    private IVehicleInput vehicleInput;
    private Team team = Team.Player;
    private AudioSource[] fireVoices;   // round-robin pool so overlapping shots don't clip each other
    private int voiceIndex;
    private int lastShotIndex = -1;
    private int obstacleMask;

    float CurrentFireRate => weaponType == WeaponType.RocketLauncher ? rocketFireRate : fireRate;

    void Awake()
    {
        vehicleInput = GetComponent<IVehicleInput>();
        obstacleMask = LayerMask.GetMask("Default");

        TeamMember member = GetComponentInParent<TeamMember>();
        if (member != null)
        {
            team = member.team;
        }

        SetupAudio();
    }

    void SetupAudio()
    {
        if (fireShots == null || fireShots.Length == 0)
        {
            fireShots = Resources.LoadAll<AudioClip>("Audio/Fire");
        }

        // 5 voices: at fireRate 0.25s a voice is only reused every ~1.25s, so
        // each shot's ~1.3s tail rings almost fully before it's recycled.
        fireVoices = new AudioSource[5];
        for (int i = 0; i < fireVoices.Length; i++)
        {
            AudioSource a = gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 0f; // 2D: consistent punch regardless of the pulled-back camera
            fireVoices[i] = a;
        }
    }

    void Update()
    {
        if (inputLocked) return;

        cooldown -= Time.deltaTime;

        Vector2 aimInput = GetAimInput();

        if (aimInput.magnitude > fireDeadzone)
        {
            AimFirePoint(aimInput);

            if (cooldown <= 0f)
            {
                Shoot();
                cooldown = CurrentFireRate;
            }
        }
    }

    void PlayFireShot()
    {
        if (fireVoices == null || fireShots == null || fireShots.Length == 0) return;

        int ix = fireShots.Length == 1 ? 0 : Random.Range(0, fireShots.Length);
        if (fireShots.Length > 1 && ix == lastShotIndex) ix = (ix + 1) % fireShots.Length;
        lastShotIndex = ix;

        AudioClip clip = fireShots[ix];
        if (clip == null) return;

        AudioSource v = fireVoices[voiceIndex];
        voiceIndex = (voiceIndex + 1) % fireVoices.Length;
        v.pitch = firePitch * (1f + Random.Range(-firePitchJitter, firePitchJitter));
        v.PlayOneShot(clip, fireVolume * (1f + Random.Range(-fireVolumeJitter, fireVolumeJitter)));
    }

    Vector2 GetAimInput()
    {
        if (vehicleInput != null)
        {
            return vehicleInput.AimInput;
        }
        if (aimJoystick != null)
        {
            return aimJoystick.InputVector;
        }
        return Input.GetButton("Fire1") ? Vector2.up : Vector2.zero;
    }

    void AimFirePoint(Vector2 aimInput)
    {
        // Keyboard fallback (no IVehicleInput, no stick) just fires straight ahead.
        if (vehicleInput == null && aimJoystick == null) return;

        Vector3 aimDirection = new Vector3(aimInput.x, 0f, aimInput.y);
        if (aimDirection.sqrMagnitude > 0.0001f)
        {
            firePoint.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
        }
    }

    void Shoot()
    {
        if (firePoint == null) return;

        if (weaponType == WeaponType.RocketLauncher)
        {
            if (rocketPrefab == null) return;

            GameObject go = Instantiate(rocketPrefab, firePoint.position, firePoint.rotation);
            if (go.TryGetComponent(out Rocket rocket))
            {
                rocket.team = team;
                rocket.target = FindRocketTarget(firePoint.forward); // null = flies straight, never a wasted shot
            }
        }
        else
        {
            if (projectilePrefab == null) return;

            GameObject bullet = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            if (bullet.TryGetComponent(out Projectile projectile))
            {
                projectile.team = team;
            }
        }

        PlayFireShot();
    }

    // Nearest-to-centre living enemy within range/cone of the aim direction,
    // with line of sight, or null. Only called at the instant a rocket fires.
    Health FindRocketTarget(Vector3 aimDir)
    {
        Health best = null;
        float bestAngle = rocketTargetConeHalfAngle;

        foreach (TeamMember tm in FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            if (tm.team == team) continue;

            Health h = tm.GetComponent<Health>();
            if (h == null || h.IsDead) continue;

            Respawner rs = tm.GetComponent<Respawner>();
            if (rs != null && rs.IsDead) continue;

            Vector3 toTarget = h.transform.position - firePoint.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f || toTarget.magnitude > rocketTargetRange) continue;

            float angle = Vector3.Angle(aimDir, toTarget);
            if (angle > bestAngle) continue;

            if (Physics.Linecast(firePoint.position, h.transform.position, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                continue; // something solid blocks the view
            }

            bestAngle = angle;
            best = h;
        }
        return best;
    }
}
