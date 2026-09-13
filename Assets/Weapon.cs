using UnityEngine;

// ATTACH THIS TO: your vehicle (same as before - no need to re-add).
// The aim direction points a "turret" independently of the car's facing, and
// firing happens automatically while aim is pushed past fireDeadzone.
// Input source, in priority order: an IVehicleInput component on this object
// (player joystick rig or EnemyDriverAI) -> the assigned aimJoystick ->
// "hold Ctrl / left mouse to fire straight ahead" for keyboard testing.
// Spawned bullets are stamped with this vehicle's Team so they don't hit allies.
// Every bullet fires one discrete gunshot sample (fireShots) - no baked "spray"
// loop; holding the trigger just retriggers shots at fireRate. Each sample is a
// real .50-cal shot with a ~1.3s decay tail; on a pool of 2D AudioSources so the
// tails of consecutive shots overlap and build the "wall" during a spray while a
// single tap still rings out fully. Per shot picks a random clip (never the same
// one twice running) with small pitch/volume jitter. Clips auto-load from
// Resources/Audio/Fire if left empty.
public class Weapon : MonoBehaviour
{
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float fireRate = 0.25f;

    [Header("Touch Input (optional)")]
    public VirtualJoystick aimJoystick;
    public float fireDeadzone = 0.3f;

    [Header("Audio")]
    public AudioClip[] fireShots;                          // one played per bullet; empty -> Resources/Audio/Fire
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

    void Awake()
    {
        vehicleInput = GetComponent<IVehicleInput>();

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
                cooldown = fireRate;
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
        if (projectilePrefab == null || firePoint == null) return;

        GameObject bullet = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        if (bullet.TryGetComponent(out Projectile projectile))
        {
            projectile.team = team;
        }

        PlayFireShot();
    }
}