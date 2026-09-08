using UnityEngine;

// ATTACH THIS TO: your vehicle GameObject (same as before - no need to re-add).
// UPDATED: the car now turns to directly face whichever direction the stick
// (or WASD) points, instead of steering left/right relative to itself.
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 12f;
    public float turnSmoothing = 8f; // how quickly the car rotates to face the input direction
    public float acceleration = 8f;

    [Header("Touch Input (optional)")]
    public VirtualJoystick movementJoystick;

    private Rigidbody rb;
    private float currentSpeed = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
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

        float targetSpeed = inputMagnitude > 0.1f ? moveSpeed * inputMagnitude : 0f;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

        Vector3 forwardMove = transform.forward * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + forwardMove);
    }

    Vector2 GetMovementInput()
    {
        if (movementJoystick != null)
        {
            return movementJoystick.InputVector;
        }
        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }
}