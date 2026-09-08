using UnityEngine;

// ATTACH THIS TO: your vehicle (same as before - no need to re-add).
// The aim direction points a "turret" independently of the car's facing, and
// firing happens automatically while aim is pushed past fireDeadzone.
// Input source, in priority order: an IVehicleInput component on this object
// (player joystick rig or EnemyDriverAI) -> the assigned aimJoystick ->
// "hold Ctrl / left mouse to fire straight ahead" for keyboard testing.
// Spawned bullets are stamped with this vehicle's Team so they don't hit allies.
public class Weapon : MonoBehaviour
{
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float fireRate = 0.25f;

    [Header("Touch Input (optional)")]
    public VirtualJoystick aimJoystick;
    public float fireDeadzone = 0.3f;

    private float cooldown = 0f;
    private IVehicleInput vehicleInput;
    private Team team = Team.Player;

    void Awake()
    {
        vehicleInput = GetComponent<IVehicleInput>();

        TeamMember member = GetComponentInParent<TeamMember>();
        if (member != null)
        {
            team = member.team;
        }
    }

    void Update()
    {
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
    }
}