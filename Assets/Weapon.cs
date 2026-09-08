using UnityEngine;

// ATTACH THIS TO: your vehicle (same as before - no need to re-add).
// The right stick aims independently of the car's facing direction (a real
// "turret"), and firing happens automatically while the stick is pushed past
// a deadzone. Falls back to "hold Ctrl / left mouse to fire straight ahead"
// if no aimJoystick is assigned, so keyboard testing still works.
public class Weapon : MonoBehaviour
{
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float fireRate = 0.25f;

    [Header("Touch Input (optional)")]
    public VirtualJoystick aimJoystick;
    public float fireDeadzone = 0.3f;

    private float cooldown = 0f;

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
        if (aimJoystick != null)
        {
            return aimJoystick.InputVector;
        }
        return Input.GetButton("Fire1") ? Vector2.up : Vector2.zero;
    }

    void AimFirePoint(Vector2 aimInput)
    {
        if (aimJoystick == null) return;

        Vector3 aimDirection = new Vector3(aimInput.x, 0f, aimInput.y);
        firePoint.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
    }

    void Shoot()
    {
        if (projectilePrefab == null || firePoint == null) return;
        Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
    }
}