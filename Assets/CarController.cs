using System;
using UnityEngine;

// ATTACH THIS TO: your vehicle GameObject (same as before - no need to re-add).
// The car turns to directly face whichever direction the input points, instead
// of steering left/right relative to itself.
// Input source, in priority order: an IVehicleInput component on this object
// (player joystick rig or EnemyDriverAI) -> the assigned movementJoystick ->
// keyboard WASD. The enemy uses the same code path, so movement stats match.
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 12f;
    public float turnSmoothing = 8f; // how quickly the car rotates to face the input direction
    public float acceleration = 8f;

    [Header("Touch Input (optional)")]
    public VirtualJoystick movementJoystick;

    // 1 = normal. Flag sets this below 1 while this car is carrying an enemy
    // flag in Capture the Flag, and restores it to 1 on drop/score/death.
    [NonSerialized] public float speedMultiplier = 1f;

    // false = normal. MatchModeManager sets this true after a match ends, so
    // the car coasts to a stop under its own accel/decel curve instead of
    // just freezing in place - input is ignored but FixedUpdate keeps running.
    [NonSerialized] public bool inputLocked = false;

    private Rigidbody rb;
    private float currentSpeed = 0f;
    private IVehicleInput vehicleInput;

    // Current forward speed after accel/decel smoothing. EngineAudio maps this to
    // engine RPM; divide by moveSpeed for a 0..1 "throttle load".
    public float CurrentSpeed => currentSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        vehicleInput = GetComponent<IVehicleInput>();
    }

    void FixedUpdate()
    {
        Vector2 input = GetMovementInput();
        Vector3 inputDirection = new Vector3(input.x, 0f, input.y);
        float inputMagnitude = inputDirection.magnitude;

        if (inputMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputDirection, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, turnSmoothing * Time.fixedDeltaTime));
        }

        float targetSpeed = inputMagnitude > 0.1f ? moveSpeed * speedMultiplier * inputMagnitude : 0f;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

        Vector3 forwardMove = transform.forward * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + forwardMove);
    }

    Vector2 GetMovementInput()
    {
        if (inputLocked) return Vector2.zero;
        if (vehicleInput != null)
        {
            return vehicleInput.MoveInput;
        }
        if (movementJoystick != null)
        {
            return movementJoystick.InputVector;
        }
        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }
}