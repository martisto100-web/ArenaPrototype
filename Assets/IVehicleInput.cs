using UnityEngine;

// Implemented by anything that can drive a vehicle: the player's on-screen
// joysticks, or an AI brain. CarController and Weapon read from this when a
// component implementing it is present on the same GameObject, so a human and
// an AI use the exact same movement + weapon code (and therefore the same stats).
//
// MoveInput / AimInput mirror a twin-stick layout:
//   x = right, y = forward, each roughly -1..1.
//   AimInput magnitude past Weapon.fireDeadzone means "fire".
public interface IVehicleInput
{
    Vector2 MoveInput { get; }
    Vector2 AimInput { get; }
}
