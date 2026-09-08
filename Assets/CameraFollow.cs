using UnityEngine;

// ATTACH THIS TO: your Main Camera.
// Gives a smooth, elevated, angled-down view of the vehicle (not a flat top-down view).
public class CameraFollow : MonoBehaviour
{
    public Transform target;       // drag your vehicle here in the Inspector
    public Vector3 offset = new Vector3(0f, 14f, -9f); // height and back-distance from target
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Look slightly above the vehicle's base so the framing feels natural.
        transform.LookAt(target.position + Vector3.up * 1f);
    }
}
