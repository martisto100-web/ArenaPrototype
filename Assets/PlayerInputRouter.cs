using UnityEngine;

// ATTACH THIS TO: the Wraith / player-car root (alongside CarController + Weapon).
// One IVehicleInput that the human uses on every platform. Aiming/firing is
// always the aim VirtualJoystick's direction, on mobile and PC alike -
// there's no mouse-look aim mechanic. On PC/editor the aim stick is just
// another on-screen UI element, so drag it with the mouse the same way a
// thumb would on a touchscreen.
//   - Mobile build  -> the two on-screen VirtualJoysticks (kept for release).
//   - PC / editor    -> WASD to move; aim stick is mouse-dragged like touch.
// "Auto" picks the move scheme by platform; force a mode from the Inspector
// if you want to test the move touch stick on PC too.
[RequireComponent(typeof(CarController))]
public class PlayerInputRouter : MonoBehaviour, IVehicleInput
{
    public enum Scheme { Auto, KeyboardMouse, TouchJoysticks }

    [Header("Scheme (movement only - aim always uses the joystick)")]
    public Scheme scheme = Scheme.Auto;

    [Header("Touch (filled from CarController/Weapon if left empty)")]
    public VirtualJoystick moveJoystick;
    public VirtualJoystick aimJoystick;

    [Header("Keyboard / mouse (movement only)")]
    public string horizontalAxis = "Horizontal";
    public string verticalAxis = "Vertical";

    public Vector2 MoveInput { get; private set; }
    public Vector2 AimInput { get; private set; }

    void Awake()
    {
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
        MoveInput = UseKeyboardMouse()
            ? new Vector2(Input.GetAxisRaw(horizontalAxis), Input.GetAxisRaw(verticalAxis))
            : (moveJoystick != null ? moveJoystick.InputVector : Vector2.zero);

        AimInput = aimJoystick != null ? aimJoystick.InputVector : Vector2.zero;
    }
}
