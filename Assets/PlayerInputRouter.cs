using UnityEngine;

// ATTACH THIS TO: the PlayerCar root (alongside CarController + Weapon).
// One IVehicleInput that the human uses on every platform:
//   - Mobile build  -> the two on-screen VirtualJoysticks (kept for release).
//   - PC / editor    -> WASD to move, mouse to aim, hold left mouse to fire.
// "Auto" picks by platform; force a mode from the Inspector if you want to
// test the touch sticks on PC.
[RequireComponent(typeof(CarController))]
public class PlayerInputRouter : MonoBehaviour, IVehicleInput
{
    public enum Scheme { Auto, KeyboardMouse, TouchJoysticks }

    [Header("Scheme")]
    public Scheme scheme = Scheme.Auto;

    [Header("Touch (filled from CarController/Weapon if left empty)")]
    public VirtualJoystick moveJoystick;
    public VirtualJoystick aimJoystick;

    [Header("Keyboard / mouse")]
    public string horizontalAxis = "Horizontal";
    public string verticalAxis = "Vertical";
    public int fireMouseButton = 0;

    public Vector2 MoveInput { get; private set; }
    public Vector2 AimInput { get; private set; }

    private Camera cam;

    void Awake()
    {
        cam = Camera.main;

        // Reuse the joystick refs already wired on the vehicle's own components.
        if (moveJoystick == null)
        {
            CarController car = GetComponent<CarController>();
            if (car != null) moveJoystick = car.movementJoystick;
        }
        if (aimJoystick == null)
        {
            Weapon weapon = GetComponent<Weapon>();
            if (weapon != null) aimJoystick = weapon.aimJoystick;
        }
    }

    bool UseKeyboardMouse()
    {
        switch (scheme)
        {
            case Scheme.KeyboardMouse: return true;
            case Scheme.TouchJoysticks: return false;
            default: return !Application.isMobilePlatform;
        }
    }

    void Update()
    {
        if (UseKeyboardMouse())
        {
            MoveInput = new Vector2(Input.GetAxisRaw(horizontalAxis), Input.GetAxisRaw(verticalAxis));
            AimInput = ReadMouseAim();
        }
        else
        {
            MoveInput = moveJoystick != null ? moveJoystick.InputVector : Vector2.zero;
            AimInput = aimJoystick != null ? aimJoystick.InputVector : Vector2.zero;
        }
    }

    // Aim at the mouse's point on the ground plane; only "pushed" (i.e. firing)
    // while the fire button is held, so Weapon treats it like a stick past the
    // deadzone.
    Vector2 ReadMouseAim()
    {
        if (!Input.GetMouseButton(fireMouseButton)) return Vector2.zero;
        if (cam == null) cam = Camera.main;
        if (cam == null) return Vector2.zero;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!plane.Raycast(ray, out float enter)) return Vector2.zero;

        Vector3 aimPoint = ray.GetPoint(enter);
        Vector3 dir = aimPoint - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return Vector2.zero;

        dir.Normalize();
        return new Vector2(dir.x, dir.z);
    }
}
